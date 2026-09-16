using NetScope.Core.Diagnostics;
using NetScope.Core.Models;

namespace NetScope.Tests.Diagnostics;

public class DiagnosticAnalyzerTests
{
    private readonly DiagnosticAnalyzer _analyzer = new();

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static NetworkMeasurement Sample(
        int cycle,
        double loss,
        double? latency,
        double? jitter = 1.0,
        double? gateway = null,
        double? dns = null,
        int intervalSeconds = 60)
    {
        var connected = loss < 100.0;
        var health = HealthClassifier.Classify(loss, latency, jitter);
        return new NetworkMeasurement
        {
            Timestamp = T0.AddSeconds((cycle - 1) * intervalSeconds),
            CycleNumber = cycle,
            Target = "1.1.1.1",
            IsConnected = connected,
            PacketsSent = 4,
            PacketsReceived = connected ? (int)Math.Round(4 * (1 - loss / 100.0)) : 0,
            PacketLossPercent = loss,
            MinLatencyMs = latency,
            MaxLatencyMs = latency,
            AvgLatencyMs = latency,
            JitterMs = jitter,
            GatewayLatencyMs = gateway,
            DnsResolutionMs = dns,
            HealthStatus = health
        };
    }

    private static List<NetworkMeasurement> Series(
        int count,
        Func<int, NetworkMeasurement> factory)
    {
        var list = new List<NetworkMeasurement>(count);
        for (var i = 1; i <= count; i++)
            list.Add(factory(i));
        return list;
    }

    private DiagnosticReport Analyze(IReadOnlyList<NetworkMeasurement> measurements, TracerouteResult? trace = null) =>
        _analyzer.Analyze(new DiagnosticRequest { Measurements = measurements, Traceroute = trace });

    // --- Empty / sparse ---

    [Fact]
    public void Analyze_Empty_ReturnsDataEmpty()
    {
        var report = Analyze([]);

        Assert.Equal(0, report.SampleCount);
        Assert.Equal(DiagnosticSeverity.Info, report.Severity);
        Assert.Contains(report.Findings, f => f.Code == "DATA_EMPTY");
        Assert.Contains("No measurements", report.Headline);
        Assert.Contains(report.SuggestedActions, a => a.Kind == DiagnosticActionKind.ReviewHistory);
    }

    [Fact]
    public void Analyze_NullMeasurementInList_IsIgnored()
    {
        var data = new List<NetworkMeasurement> { Sample(1, 0, 8), null!, Sample(2, 0, 9) };
        var report = Analyze(data);

        Assert.Equal(2, report.SampleCount);
        Assert.Contains(report.Findings, f => f.Code == "HEALTHY");
    }

    [Fact]
    public void Analyze_ThreeHealthySamples_SkipsTrendsAndReportsHealthy()
    {
        var data = Series(3, i => Sample(i, 0, 8));
        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "DATA_SPARSE");
        Assert.Contains(report.Findings, f => f.Code == "HEALTHY");
        Assert.DoesNotContain(report.Findings, f => f.Code == "LOSS_TREND");
        Assert.Equal(DiagnosticSeverity.Info, report.Severity);
    }

    [Fact]
    public void Analyze_FourSamplesJitterRise_SkipsTrend()
    {
        var data = Series(4, i => i <= 2
            ? Sample(i, 0, 20, 13)
            : Sample(i, 0, 25, 23));

        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "DATA_SPARSE");
        Assert.DoesNotContain(report.Findings, f => f.Code == "JITTER_TREND");
        Assert.Contains(report.Findings, f => f.Code == "JITTER_ELEVATED");
    }

    [Fact]
    public void Analyze_EightSamplesUnderTwoMinutes_SkipsTrends()
    {
        var data = Series(8, i => i <= 4
            ? Sample(i, 0.2, 12, 2, intervalSeconds: 10)
            : Sample(i, 8.4, 90, 20, intervalSeconds: 10));

        var report = Analyze(data);

        Assert.True(report.AnalyzedDuration < TimeSpan.FromMinutes(2));
        Assert.Contains(report.Findings, f => f.Code == "DATA_SPARSE");
        Assert.DoesNotContain(report.Findings, f => f.Code is "LOSS_TREND" or "LATENCY_TREND" or "JITTER_TREND");
    }

    // --- Healthy ---

    [Fact]
    public void Analyze_StableHealthySeries_NoIssues()
    {
        var data = Series(20, i => Sample(i, 0, 12, 2, gateway: 2));
        var report = Analyze(data);

        Assert.Equal(DiagnosticSeverity.Info, report.Severity);
        Assert.Contains("No connectivity issues", report.Headline);
        Assert.Contains(report.Findings, f => f.Code == "HEALTHY");
        Assert.DoesNotContain(report.Findings, f => f.Code is "LOSS_TREND" or "LATENCY_TREND");
        Assert.DoesNotContain(report.SuggestedActions, a => a.Kind == DiagnosticActionKind.RunDiagnostics);
    }

    // --- Classic instability (project example) ---

    [Fact]
    public void Analyze_LossAndLatencyRiseWithHighGateway_LocalInstabilityNarrative()
    {
        var data = Series(20, i => i <= 10
            ? Sample(i, 0.2, 12, 2, gateway: 2)
            : Sample(i, 8.4, 90, 15, gateway: 55));

        var report = Analyze(data);

        Assert.Equal("Possible network instability detected.", report.Headline);
        Assert.Equal(DiagnosticSeverity.Alert, report.Severity);

        var loss = Assert.Single(report.Findings, f => f.Code == "LOSS_TREND");
        Assert.Contains("0.2%", loss.Detail);
        Assert.Contains("8.4%", loss.Detail);
        Assert.Contains("last 10 minutes", loss.Detail);

        Assert.Contains(report.Findings, f => f.Code == "LATENCY_TREND");
        var local = Assert.Single(report.Findings, f => f.Code == "LOCALITY_LOCAL");
        Assert.Contains("closer to the local network than the destination", local.Detail);

        Assert.Contains("Packet loss increased from 0.2% to 8.4%", report.Summary);
        Assert.Contains(report.SuggestedActions, a => a.Kind == DiagnosticActionKind.RunDiagnostics);
        Assert.Contains(report.SuggestedActions, a => a.Kind == DiagnosticActionKind.CheckLocalNetwork);
        Assert.Equal("1.1.1.1", report.Target);
        Assert.Equal(20, report.SampleCount);
    }

    [Fact]
    public void Analyze_LossRiseWithLowGateway_RemoteLocality()
    {
        var data = Series(20, i => i <= 10
            ? Sample(i, 0.2, 12, 2, gateway: 1.5)
            : Sample(i, 8.4, 180, 12, gateway: 1.8));

        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "LOCALITY_REMOTE");
        Assert.Contains("closer to the destination or upstream path", report.Summary);
        Assert.DoesNotContain(report.Findings, f => f.Code == "LOCALITY_LOCAL");
    }

    // --- Trends ---

    [Fact]
    public void Analyze_LatencyDoubled_ReportsLatencyTrend()
    {
        var data = Series(10, i => i <= 5
            ? Sample(i, 0, 20, 2)
            : Sample(i, 0, 80, 2));

        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "LATENCY_TREND");
        Assert.Equal(DiagnosticSeverity.Warning, report.Severity);
        Assert.Equal("Network performance is degraded.", report.Headline);
    }

    [Fact]
    public void Analyze_JitterRise_ReportsJitterTrend()
    {
        var data = Series(10, i => i <= 5
            ? Sample(i, 0, 20, 2)
            : Sample(i, 0, 25, 18));

        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "JITTER_TREND");
        Assert.Contains("Jitter rose", report.Summary);
    }

    [Fact]
    public void Analyze_SmallLossChange_DoesNotReportTrend()
    {
        var data = Series(10, i => i <= 5
            ? Sample(i, 1.0, 20)
            : Sample(i, 2.0, 22));

        var report = Analyze(data);

        Assert.DoesNotContain(report.Findings, f => f.Code == "LOSS_TREND");
    }

    // --- Snapshot elevated ---

    [Fact]
    public void Analyze_SustainedHighLatency_ReportsElevated()
    {
        var data = Series(8, i => Sample(i, 0, 150, 4));
        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "LATENCY_ELEVATED");
        Assert.Equal(DiagnosticSeverity.Warning, report.Severity);
    }

    [Fact]
    public void Analyze_SustainedHighLoss_ReportsElevatedAlert()
    {
        var data = Series(8, i => Sample(i, 25, 40, 4));
        var report = Analyze(data);

        var loss = Assert.Single(report.Findings, f => f.Code == "LOSS_ELEVATED");
        Assert.Equal(DiagnosticSeverity.Alert, loss.Severity);
        Assert.Contains("Possible network instability", report.Headline);
    }

    // --- Connectivity ---

    [Fact]
    public void Analyze_MajorityDisconnected_ReportsOutage()
    {
        var data = Series(10, i => i <= 2
            ? Sample(i, 0, 10)
            : Sample(i, 100, null, null));

        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "OUTAGE");
        Assert.Equal(DiagnosticSeverity.Critical, report.Severity);
        Assert.Equal("Connection outage detected.", report.Headline);
        Assert.Contains(report.SuggestedActions, a => a.Kind == DiagnosticActionKind.RunDiagnostics);
    }

    [Fact]
    public void Analyze_TwoDroppedCycles_ReportsIntermittent()
    {
        var data = Series(10, i => i is 4 or 8
            ? Sample(i, 100, null, null)
            : Sample(i, 0, 10));

        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "INTERMITTENT");
        Assert.Contains("2 of 10", report.Summary);
    }

    [Fact]
    public void Analyze_ThreeConsecutiveDrops_ReportsOutageAlert()
    {
        var data = Series(10, i => i is >= 5 and <= 7
            ? Sample(i, 100, null, null)
            : Sample(i, 0, 10));

        var report = Analyze(data);

        var outage = Assert.Single(report.Findings, f => f.Code == "OUTAGE");
        Assert.Equal(DiagnosticSeverity.Alert, outage.Severity);
        Assert.Contains("3 consecutive", outage.Detail);
    }

    // --- DNS ---

    [Fact]
    public void Analyze_SlowDnsWithLowPing_ReportsDns()
    {
        var data = Series(8, i => Sample(i, 0, 15, 2, dns: 350));
        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "DNS_SLOW");
        Assert.Equal("DNS resolution is slow.", report.Headline);
        Assert.Contains("name resolution", report.Summary);
    }

    [Fact]
    public void Analyze_SlowDnsWithHighPing_DoesNotBlameDnsAlone()
    {
        var data = Series(8, i => Sample(i, 0, 180, 4, dns: 350));
        var report = Analyze(data);

        Assert.DoesNotContain(report.Findings, f => f.Code == "DNS_SLOW");
        Assert.Contains(report.Findings, f => f.Code == "LATENCY_ELEVATED");
    }

    // --- Traceroute ---

    [Fact]
    public void Analyze_FirstHopTimeout_ReportsLocalTrace()
    {
        var data = Series(8, i => Sample(i, 20, 200));
        var trace = new TracerouteResult
        {
            Target = "1.1.1.1",
            Hops =
            [
                new TracerouteHop { HopNumber = 1, Responded = false, Status = "TimedOut" }
            ],
            DestinationReached = false,
            TotalDurationMs = 3000,
            Timestamp = T0
        };

        var report = Analyze(data, trace);

        Assert.Contains(report.Findings, f => f.Code == "TRACE_FIRST_HOP");
        Assert.Contains(report.SuggestedActions, a => a.Kind == DiagnosticActionKind.CheckLocalNetwork);
    }

    [Fact]
    public void Analyze_IncompleteTraceWithUnhealthyDest_ReportsUpstream()
    {
        var data = Series(8, i => Sample(i, 15, 120, gateway: 3));
        var trace = new TracerouteResult
        {
            Target = "1.1.1.1",
            Hops =
            [
                new TracerouteHop { HopNumber = 1, Address = "192.168.1.1", Responded = true, Status = "TtlExpired", RoundTripTimeMs = 2 },
                new TracerouteHop { HopNumber = 2, Address = "10.0.0.1", Responded = true, Status = "TtlExpired", RoundTripTimeMs = 12 },
                new TracerouteHop { HopNumber = 3, Responded = false, Status = "TimedOut" }
            ],
            DestinationReached = false,
            TotalDurationMs = 9000,
            Timestamp = T0
        };

        var report = Analyze(data, trace);

        Assert.Contains(report.Findings, f => f.Code == "TRACE_INCOMPLETE");
    }

    // --- Recovery ---

    [Fact]
    public void Analyze_EarlyLossThenHealthy_ReportsRecovery()
    {
        var data = Series(10, i => i <= 5
            ? Sample(i, 25, 40)
            : Sample(i, 0, 12, 2));

        var report = Analyze(data);

        Assert.Contains(report.Findings, f => f.Code == "RECOVERY");
        Assert.Contains("improved", report.Headline, StringComparison.OrdinalIgnoreCase);
    }

    // --- Metadata ---

    [Fact]
    public void Analyze_SetsPeriodAndDuration()
    {
        var data = Series(10, i => Sample(i, 0, 10));
        var report = Analyze(data);

        Assert.Equal(T0, report.PeriodStart);
        Assert.Equal(T0.AddMinutes(9), report.PeriodEnd);
        Assert.Equal(TimeSpan.FromMinutes(9), report.AnalyzedDuration);
    }

    [Fact]
    public void Analyze_UnsortedInput_OrdersByTimestamp()
    {
        var data = new List<NetworkMeasurement>
        {
            Sample(8, 8.4, 90, gateway: 60),
            Sample(1, 0.2, 12, gateway: 2),
            Sample(2, 0.2, 12, gateway: 2),
            Sample(5, 8.4, 90, gateway: 60),
            Sample(3, 0.2, 12, gateway: 2),
            Sample(6, 8.4, 90, gateway: 60),
            Sample(4, 0.2, 12, gateway: 2),
            Sample(7, 8.4, 90, gateway: 60)
        };

        var report = Analyze(data);

        Assert.Equal(T0, report.PeriodStart);
        Assert.Contains(report.Findings, f => f.Code == "LOSS_TREND");
    }

    [Fact]
    public void Analyze_Healthy_SuggestsReviewHistoryOnly()
    {
        var data = Series(8, i => Sample(i, 0, 8));
        var report = Analyze(data);

        Assert.Single(report.SuggestedActions);
        Assert.Equal(DiagnosticActionKind.ReviewHistory, report.SuggestedActions[0].Kind);
    }

    [Fact]
    public void Analyze_FindingsOrderedBySeverityDescending()
    {
        var data = Series(10, i => i <= 5
            ? Sample(i, 0.2, 12, gateway: 2)
            : Sample(i, 40, 90, gateway: 80));

        var report = Analyze(data);

        for (var i = 1; i < report.Findings.Count; i++)
            Assert.True(report.Findings[i - 1].Severity >= report.Findings[i].Severity);
    }
}
