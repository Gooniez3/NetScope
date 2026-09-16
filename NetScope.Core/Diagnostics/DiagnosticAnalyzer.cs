using NetScope.Core.Models;

namespace NetScope.Core.Diagnostics;

/// <summary>
/// Rule-based diagnostic engine. Pure functions over measurements — no I/O, fully unit-testable.
/// </summary>
/// <remarks>
/// <para>
/// Splits the sample series in half to detect trends (loss, latency, jitter), classifies
/// snapshot severity, and uses gateway RTT / traceroute hops to distinguish local-network
/// problems from upstream or destination issues.
/// </para>
/// <para>
/// Trend evaluation requires at least <see cref="MinSamplesForTrend"/> measurements
/// spanning <see cref="MinDurationForTrend"/>. Short sessions are classified from
/// snapshot averages only — they are not described as a trend.
/// Classification thresholds align with <see cref="HealthClassifier"/> defaults.
/// </para>
/// </remarks>
public sealed class DiagnosticAnalyzer : IDiagnosticAnalyzer
{
    /// <summary>Minimum samples required before early/late trend comparison runs.</summary>
    public const int MinSamplesForTrend = 8;

    /// <summary>Minimum session span required before early/late trend comparison runs.</summary>
    public static readonly TimeSpan MinDurationForTrend = TimeSpan.FromMinutes(2);

    /// <summary>Packet-loss increase (percentage points) that counts as a trend.</summary>
    public const double LossTrendDeltaPercent = 3.0;

    /// <summary>Absolute latency increase (ms) that counts as a trend.</summary>
    public const double LatencyTrendDeltaMs = 30.0;

    /// <summary>Relative latency increase (late / early) that counts as a trend when also ≥ 20 ms.</summary>
    public const double LatencyTrendRatio = 1.5;

    /// <summary>Gateway RTT (ms) treated as healthy for locality decisions.</summary>
    public const double GatewayHealthyMs = 20.0;

    /// <summary>Gateway RTT (ms) treated as elevated / local-network evidence.</summary>
    public const double GatewayElevatedMs = 50.0;

    /// <summary>DNS resolution (ms) treated as slow when destination ping is otherwise reasonable.</summary>
    public const double DnsSlowMs = 200.0;

    /// <inheritdoc />
    public DiagnosticReport Analyze(DiagnosticRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Measurements);

        var ordered = request.Measurements
            .Where(m => m is not null)
            .OrderBy(m => m.Timestamp)
            .ThenBy(m => m.CycleNumber)
            .ToList();

        if (ordered.Count == 0)
            return BuildEmpty();

        var target = ordered[0].Target;
        var periodStart = ordered[0].Timestamp;
        var periodEnd = ordered[^1].Timestamp;
        var duration = periodEnd - periodStart;
        var overall = ComputeMetrics(ordered);

        var findings = new List<DiagnosticFinding>();

        var canEvaluateTrends = CanEvaluateTrends(ordered.Count, duration);
        if (!canEvaluateTrends)
            findings.Add(SparseFinding(ordered.Count, duration));

        AddConnectivityFindings(ordered, overall, findings);

        WindowMetrics? early = null;
        WindowMetrics? late = null;

        if (canEvaluateTrends)
        {
            var mid = ordered.Count / 2;
            early = ComputeMetrics(ordered.Take(mid).ToList());
            late = ComputeMetrics(ordered.Skip(mid).ToList());
            var windowPhrase = DescribeWindow(late, duration);
            AddTrendFindings(early, late, windowPhrase, findings);
        }

        AddSnapshotFindings(late ?? overall, findings);
        AddDnsFinding(overall, findings);
        AddLocalityFindings(late ?? overall, overall, request.Traceroute, findings);
        AddRecoveryFinding(early, late, findings);

        DeduplicateByCode(findings);

        if (findings.Count == 0 || findings.All(f => f.Code is "DATA_SPARSE" or "HEALTHY" or "RECOVERY"))
        {
            if (findings.All(f => f.Code != "HEALTHY"))
                findings.Add(HealthyFinding(overall));
        }

        findings.Sort(CompareFindings);

        var severity = findings.Count == 0
            ? DiagnosticSeverity.Info
            : findings.Max(f => f.Severity);

        var headline = BuildHeadline(severity, findings);
        var summary = BuildSummary(headline, findings);
        var actions = BuildActions(severity, findings, target);

        return new DiagnosticReport
        {
            Headline = headline,
            Summary = summary,
            Severity = severity,
            Target = target,
            SampleCount = ordered.Count,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AnalyzedDuration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration,
            AvgLatencyMs = overall.AvgLatency,
            AvgPacketLossPercent = overall.AvgLoss,
            AvgJitterMs = overall.AvgJitter,
            Findings = findings,
            SuggestedActions = actions
        };
    }

    private static DiagnosticReport BuildEmpty() => new()
    {
        Headline = "No measurements to analyze.",
        Summary = "No measurements to analyze. Run a monitoring session with persistence enabled, then analyze that session.",
        Severity = DiagnosticSeverity.Info,
        Target = "",
        SampleCount = 0,
        AnalyzedDuration = TimeSpan.Zero,
        Findings =
        [
            new DiagnosticFinding
            {
                Code = "DATA_EMPTY",
                Title = "No data",
                Detail = "No measurements were provided. Save a monitoring session and try again.",
                Severity = DiagnosticSeverity.Info,
                Category = DiagnosticCategory.Data
            }
        ],
        SuggestedActions =
        [
            new DiagnosticAction
            {
                Kind = DiagnosticActionKind.ReviewHistory,
                Label = "Review History",
                Reason = "Open History to confirm a saved session exists."
            }
        ]
    };

    private static void AddConnectivityFindings(
        IReadOnlyList<NetworkMeasurement> ordered,
        WindowMetrics overall,
        List<DiagnosticFinding> findings)
    {
        var longest = LongestDisconnectStreak(ordered);
        var disconnected = overall.DisconnectedCount;
        var total = overall.Count;

        if (disconnected == 0)
            return;

        var pct = disconnected / (double)total * 100.0;

        if (pct >= 50.0 || longest >= 5)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "OUTAGE",
                Title = "Connection outage",
                Detail = longest >= 2
                    ? $"Connection dropped for {longest} consecutive cycle(s). {disconnected} of {total} samples ({pct:F0}%) were unreachable."
                    : $"{disconnected} of {total} samples ({pct:F0}%) were unreachable.",
                Severity = DiagnosticSeverity.Critical,
                Category = DiagnosticCategory.Connectivity
            });
            return;
        }

        if (longest >= 3)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "OUTAGE",
                Title = "Connection drop",
                Detail = $"Connection dropped for {longest} consecutive cycle(s).",
                Severity = DiagnosticSeverity.Alert,
                Category = DiagnosticCategory.Connectivity
            });
            return;
        }

        findings.Add(new DiagnosticFinding
        {
            Code = "INTERMITTENT",
            Title = "Intermittent connectivity",
            Detail = $"Intermittent connectivity: {disconnected} of {total} cycles failed.",
            Severity = pct >= 20 ? DiagnosticSeverity.Alert : DiagnosticSeverity.Warning,
            Category = DiagnosticCategory.Connectivity
        });
    }

    private static void AddTrendFindings(
        WindowMetrics early,
        WindowMetrics late,
        string windowPhrase,
        List<DiagnosticFinding> findings)
    {
        var lossDelta = late.AvgLoss - early.AvgLoss;
        if (lossDelta >= LossTrendDeltaPercent && late.AvgLoss >= 1.0)
        {
            var severity = late.AvgLoss >= 50 ? DiagnosticSeverity.Critical
                : late.AvgLoss >= 10 ? DiagnosticSeverity.Alert
                : DiagnosticSeverity.Warning;

            findings.Add(new DiagnosticFinding
            {
                Code = "LOSS_TREND",
                Title = "Packet loss increased",
                Detail = $"Packet loss increased from {early.AvgLoss:F1}% to {late.AvgLoss:F1}% {windowPhrase}.",
                Severity = severity,
                Category = DiagnosticCategory.PacketLoss
            });
        }

        if (early.AvgLatency.HasValue && late.AvgLatency.HasValue)
        {
            var increase = late.AvgLatency.Value - early.AvgLatency.Value;
            var ratioHit = early.AvgLatency.Value > 0
                           && late.AvgLatency.Value >= early.AvgLatency.Value * LatencyTrendRatio
                           && increase >= 20.0;
            var absHit = increase >= LatencyTrendDeltaMs;

            if (ratioHit || absHit)
            {
                var severity = late.AvgLatency.Value > 500 ? DiagnosticSeverity.Alert : DiagnosticSeverity.Warning;
                findings.Add(new DiagnosticFinding
                {
                    Code = "LATENCY_TREND",
                    Title = "Latency increased",
                    Detail = $"Average latency rose from {early.AvgLatency.Value:F1} ms to {late.AvgLatency.Value:F1} ms {windowPhrase}.",
                    Severity = severity,
                    Category = DiagnosticCategory.Latency
                });
            }
        }

        if (early.AvgJitter.HasValue && late.AvgJitter.HasValue)
        {
            var increase = late.AvgJitter.Value - early.AvgJitter.Value;
            if (increase >= 15.0 && late.AvgJitter.Value >= 10.0)
            {
                var severity = late.AvgJitter.Value > 50 ? DiagnosticSeverity.Alert : DiagnosticSeverity.Warning;
                findings.Add(new DiagnosticFinding
                {
                    Code = "JITTER_TREND",
                    Title = "Jitter increased",
                    Detail = $"Jitter rose from {early.AvgJitter.Value:F1} ms to {late.AvgJitter.Value:F1} ms {windowPhrase}.",
                    Severity = severity,
                    Category = DiagnosticCategory.Jitter
                });
            }
        }
    }

    private static void AddSnapshotFindings(
        WindowMetrics focus,
        List<DiagnosticFinding> findings)
    {
        if (findings.Any(f => f.Code == "LOSS_TREND"))
        {
            // Trend already covers packet loss.
        }
        else if (focus.AvgLoss >= 10.0)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "LOSS_ELEVATED",
                Title = "Elevated packet loss",
                Detail = $"Average packet loss is {focus.AvgLoss:F1}%.",
                Severity = focus.AvgLoss >= 50 ? DiagnosticSeverity.Critical : DiagnosticSeverity.Alert,
                Category = DiagnosticCategory.PacketLoss
            });
        }
        else if (focus.AvgLoss > 0.0 && focus.AvgLoss < 10.0 && findings.All(f => f.Code != "OUTAGE"))
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "LOSS_ELEVATED",
                Title = "Minor packet loss",
                Detail = $"Average packet loss is {focus.AvgLoss:F1}%.",
                Severity = DiagnosticSeverity.Warning,
                Category = DiagnosticCategory.PacketLoss
            });
        }

        if (findings.Any(f => f.Code == "LATENCY_TREND"))
        {
            // Trend already covers latency.
        }
        else if (focus.AvgLatency is >= 500)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "LATENCY_ELEVATED",
                Title = "High latency",
                Detail = $"Average latency is {focus.AvgLatency.Value:F1} ms.",
                Severity = DiagnosticSeverity.Alert,
                Category = DiagnosticCategory.Latency
            });
        }
        else if (focus.AvgLatency is >= 100)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "LATENCY_ELEVATED",
                Title = "Elevated latency",
                Detail = $"Average latency is {focus.AvgLatency.Value:F1} ms.",
                Severity = DiagnosticSeverity.Warning,
                Category = DiagnosticCategory.Latency
            });
        }

        if (findings.Any(f => f.Code == "JITTER_TREND"))
        {
            // Trend already covers jitter.
        }
        else if (focus.AvgJitter is > 50)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "JITTER_ELEVATED",
                Title = "High jitter",
                Detail = $"Average jitter is {focus.AvgJitter.Value:F1} ms.",
                Severity = DiagnosticSeverity.Alert,
                Category = DiagnosticCategory.Jitter
            });
        }
        else if (focus.AvgJitter is >= 10)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "JITTER_ELEVATED",
                Title = "Elevated jitter",
                Detail = $"Average jitter is {focus.AvgJitter.Value:F1} ms.",
                Severity = DiagnosticSeverity.Warning,
                Category = DiagnosticCategory.Jitter
            });
        }
    }

    private static void AddDnsFinding(WindowMetrics overall, List<DiagnosticFinding> findings)
    {
        if (!overall.AvgDns.HasValue)
            return;

        var pingOk = overall.AvgLatency is null or < 100;
        if (overall.AvgDns.Value >= DnsSlowMs && pingOk)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "DNS_SLOW",
                Title = "Slow DNS resolution",
                Detail = $"DNS resolution averaged {overall.AvgDns.Value:F0} ms while destination latency stayed low. The delay is likely in name resolution rather than the data path.",
                Severity = DiagnosticSeverity.Warning,
                Category = DiagnosticCategory.Dns
            });
        }
    }

    private static void AddLocalityFindings(
        WindowMetrics focus,
        WindowMetrics overall,
        TracerouteResult? traceroute,
        List<DiagnosticFinding> findings)
    {
        var destUnhealthy = focus.AvgLoss >= 3.0
                            || (focus.AvgLatency.HasValue && focus.AvgLatency.Value >= 100.0)
                            || focus.DisconnectedCount > 0
                            || focus.UnstableCount > 0
                            || focus.DegradedCount >= Math.Max(1, focus.Count / 2);

        if (traceroute is not null)
            AddTracerouteLocality(traceroute, destUnhealthy, findings);

        if (!focus.AvgGateway.HasValue && !overall.AvgGateway.HasValue)
            return;

        if (!destUnhealthy)
            return;

        if (findings.Any(f => f.Code.StartsWith("LOCALITY", StringComparison.Ordinal)))
            return;

        var gateway = focus.AvgGateway ?? overall.AvgGateway;
        if (!gateway.HasValue)
            return;

        if (gateway.Value >= GatewayElevatedMs)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "LOCALITY_LOCAL",
                Title = "Local network likely",
                Detail = $"Gateway latency is elevated ({gateway.Value:F1} ms). The issue appears closer to the local network than the destination.",
                Severity = DiagnosticSeverity.Alert,
                Category = DiagnosticCategory.Locality
            });
            return;
        }

        if (gateway.Value < GatewayHealthyMs)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "LOCALITY_REMOTE",
                Title = "Upstream path likely",
                Detail = $"Gateway latency remained low ({gateway.Value:F1} ms). The issue appears closer to the destination or upstream path than the local network.",
                Severity = DiagnosticSeverity.Warning,
                Category = DiagnosticCategory.Locality
            });
        }
    }

    private static void AddTracerouteLocality(
        TracerouteResult traceroute,
        bool destUnhealthy,
        List<DiagnosticFinding> findings)
    {
        if (traceroute.Hops.Count == 0)
            return;

        var first = traceroute.Hops[0];
        if (!first.Responded)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "TRACE_FIRST_HOP",
                Title = "First hop did not respond",
                Detail = "The first traceroute hop timed out. The problem is likely on the local adapter, Wi-Fi/cable, or default gateway.",
                Severity = DiagnosticSeverity.Alert,
                Category = DiagnosticCategory.Locality
            });
            return;
        }

        if (!traceroute.DestinationReached && destUnhealthy)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "TRACE_INCOMPLETE",
                Title = "Trace did not reach destination",
                Detail = "Traceroute left the local network but did not reach the destination. The issue is likely on the upstream path.",
                Severity = DiagnosticSeverity.Warning,
                Category = DiagnosticCategory.Locality
            });
        }
    }

    private static void AddRecoveryFinding(WindowMetrics? early, WindowMetrics? late, List<DiagnosticFinding> findings)
    {
        if (early is null || late is null)
            return;

        var hadProblem = early.AvgLoss >= 5.0 || early.DisconnectedCount > 0
                         || (early.AvgLatency.HasValue && early.AvgLatency.Value >= 100);
        var nowHealthy = late.AvgLoss < 1.0 && late.DisconnectedCount == 0
                         && (late.AvgLatency is null or < 100)
                         && (late.AvgJitter is null or < 10);

        if (hadProblem && nowHealthy)
        {
            findings.Add(new DiagnosticFinding
            {
                Code = "RECOVERY",
                Title = "Conditions improved",
                Detail = "Later samples recovered: packet loss and latency returned to healthy levels.",
                Severity = DiagnosticSeverity.Info,
                Category = DiagnosticCategory.Connectivity
            });
        }
    }

    private static DiagnosticFinding HealthyFinding(WindowMetrics overall)
    {
        var latency = overall.AvgLatency.HasValue ? $"{overall.AvgLatency.Value:F1} ms" : "n/a";
        var jitter = overall.AvgJitter.HasValue ? $"{overall.AvgJitter.Value:F1} ms" : "n/a";
        return new DiagnosticFinding
        {
            Code = "HEALTHY",
            Title = "Healthy",
            Detail = $"Average latency {latency}, packet loss {overall.AvgLoss:F1}%, jitter {jitter}.",
            Severity = DiagnosticSeverity.Info,
            Category = DiagnosticCategory.Connectivity
        };
    }

    private static string BuildHeadline(DiagnosticSeverity severity, List<DiagnosticFinding> findings)
    {
        if (findings.Any(f => f.Code == "OUTAGE"))
            return "Connection outage detected.";

        if (findings.Any(f => f.Code is "LOSS_TREND" or "INTERMITTENT")
            && severity >= DiagnosticSeverity.Warning)
            return "Possible network instability detected.";

        if (findings.Any(f => f.Code == "DNS_SLOW") && findings.All(f => f.Code is "DNS_SLOW" or "DATA_SPARSE" or "HEALTHY"))
            return "DNS resolution is slow.";

        return severity switch
        {
            DiagnosticSeverity.Critical => "Severe network problem detected.",
            DiagnosticSeverity.Alert => "Possible network instability detected.",
            DiagnosticSeverity.Warning => "Network performance is degraded.",
            _ => findings.Any(f => f.Code == "RECOVERY")
                ? "Network conditions improved."
                : "No connectivity issues detected."
        };
    }

    private static string BuildSummary(string headline, List<DiagnosticFinding> findings)
    {
        var details = findings
            .Where(f => f.Code is not "HEALTHY" and not "DATA_SPARSE")
            .Take(4)
            .Select(f => f.Detail)
            .ToList();

        if (details.Count == 0)
        {
            var healthy = findings.FirstOrDefault(f => f.Code == "HEALTHY");
            return healthy is null ? headline : $"{headline} {healthy.Detail}";
        }

        return headline + " " + string.Join(" ", details);
    }

    private static IReadOnlyList<DiagnosticAction> BuildActions(
        DiagnosticSeverity severity,
        List<DiagnosticFinding> findings,
        string target)
    {
        var actions = new List<DiagnosticAction>();
        var local = findings.Any(f => f.Code is "LOCALITY_LOCAL" or "TRACE_FIRST_HOP");
        var host = string.IsNullOrWhiteSpace(target) ? "the target" : target;

        if (severity >= DiagnosticSeverity.Warning)
        {
            actions.Add(new DiagnosticAction
            {
                Kind = DiagnosticActionKind.RunDiagnostics,
                Label = "Run Diagnostics",
                Reason = $"Run ping, DNS lookup, and traceroute against {host}."
            });
        }

        if (local)
        {
            actions.Add(new DiagnosticAction
            {
                Kind = DiagnosticActionKind.CheckLocalNetwork,
                Label = "Check local network",
                Reason = "Inspect the local adapter, Wi-Fi or cable, and default gateway."
            });
        }

        if (severity == DiagnosticSeverity.Info)
        {
            actions.Add(new DiagnosticAction
            {
                Kind = DiagnosticActionKind.ReviewHistory,
                Label = "Review History",
                Reason = "Compare with earlier sessions if the problem is intermittent."
            });
        }

        return actions;
    }

    private static WindowMetrics ComputeMetrics(IReadOnlyList<NetworkMeasurement> samples)
    {
        if (samples.Count == 0)
            return new WindowMetrics();

        return new WindowMetrics
        {
            Count = samples.Count,
            AvgLoss = samples.Average(s => s.PacketLossPercent),
            AvgLatency = AverageOrNull(samples.Select(s => s.AvgLatencyMs)),
            AvgJitter = AverageOrNull(samples.Select(s => s.JitterMs)),
            AvgGateway = AverageOrNull(samples.Select(s => s.GatewayLatencyMs)),
            AvgDns = AverageOrNull(samples.Select(s => s.DnsResolutionMs)),
            DisconnectedCount = samples.Count(s => s.HealthStatus == NetworkHealthStatus.Disconnected || !s.IsConnected),
            DegradedCount = samples.Count(s => s.HealthStatus == NetworkHealthStatus.Degraded),
            UnstableCount = samples.Count(s => s.HealthStatus == NetworkHealthStatus.Unstable),
            HealthyCount = samples.Count(s => s.HealthStatus == NetworkHealthStatus.Healthy),
            Start = samples[0].Timestamp,
            End = samples[^1].Timestamp
        };
    }

    private static double? AverageOrNull(IEnumerable<double?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : present.Average();
    }

    private static int LongestDisconnectStreak(IReadOnlyList<NetworkMeasurement> ordered)
    {
        var longest = 0;
        var current = 0;
        foreach (var m in ordered)
        {
            if (!m.IsConnected || m.HealthStatus == NetworkHealthStatus.Disconnected)
            {
                current++;
                if (current > longest)
                    longest = current;
            }
            else
            {
                current = 0;
            }
        }

        return longest;
    }

    private static bool CanEvaluateTrends(int sampleCount, TimeSpan duration) =>
        sampleCount >= MinSamplesForTrend && duration >= MinDurationForTrend;

    private static DiagnosticFinding SparseFinding(int sampleCount, TimeSpan duration)
    {
        if (sampleCount < MinSamplesForTrend)
        {
            return new DiagnosticFinding
            {
                Code = "DATA_SPARSE",
                Title = "Limited sample size",
                Detail = $"Only {sampleCount} measurement(s) available. Trends were not evaluated.",
                Severity = DiagnosticSeverity.Info,
                Category = DiagnosticCategory.Data
            };
        }

        return new DiagnosticFinding
        {
            Code = "DATA_SPARSE",
            Title = "Session too short",
            Detail = "Session spans less than 2 minutes. Trends were not evaluated.",
            Severity = DiagnosticSeverity.Info,
            Category = DiagnosticCategory.Data
        };
    }

    private static string DescribeWindow(WindowMetrics late, TimeSpan totalDuration)
    {
        if (late.Start is null || late.End is null)
            return "in the later samples";

        var span = late.End.Value - late.Start.Value;
        var minutes = span.TotalMinutes;

        if (minutes >= 8 && minutes <= 12)
            return "during the last 10 minutes";
        if (minutes >= 1.5)
            return $"during the last {Math.Round(minutes)} minutes";
        if (totalDuration.TotalMinutes >= 1)
            return "in the later portion of the session";
        return "in the later samples";
    }

    private static void DeduplicateByCode(List<DiagnosticFinding> findings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = findings.Count - 1; i >= 0; i--)
        {
            if (!seen.Add(findings[i].Code))
                findings.RemoveAt(i);
        }
    }

    private static int CompareFindings(DiagnosticFinding a, DiagnosticFinding b)
    {
        var severity = b.Severity.CompareTo(a.Severity);
        if (severity != 0)
            return severity;
        return string.CompareOrdinal(a.Code, b.Code);
    }

    private sealed class WindowMetrics
    {
        public int Count { get; init; }
        public double AvgLoss { get; init; }
        public double? AvgLatency { get; init; }
        public double? AvgJitter { get; init; }
        public double? AvgGateway { get; init; }
        public double? AvgDns { get; init; }
        public int DisconnectedCount { get; init; }
        public int DegradedCount { get; init; }
        public int UnstableCount { get; init; }
        public int HealthyCount { get; init; }
        public DateTimeOffset? Start { get; init; }
        public DateTimeOffset? End { get; init; }
    }
}
