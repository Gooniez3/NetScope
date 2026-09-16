namespace NetScope.Core.Diagnostics;

/// <summary>
/// Structured explanation of network health over a set of measurements.
/// </summary>
public sealed class DiagnosticReport
{
    /// <summary>One-line conclusion (e.g. "Possible network instability detected.").</summary>
    public required string Headline { get; init; }

    /// <summary>
    /// Full narrative: headline plus the most important finding details.
    /// Suitable for CLI output or a summary paragraph in the UI.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>Worst severity among all findings.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Target host taken from the measurements, or empty when none exist.</summary>
    public required string Target { get; init; }

    /// <summary>Number of measurements analyzed.</summary>
    public required int SampleCount { get; init; }

    /// <summary>Timestamp of the earliest sample, or null when empty.</summary>
    public DateTimeOffset? PeriodStart { get; init; }

    /// <summary>Timestamp of the latest sample, or null when empty.</summary>
    public DateTimeOffset? PeriodEnd { get; init; }

    /// <summary>Elapsed time from first to last sample.</summary>
    public required TimeSpan AnalyzedDuration { get; init; }

    /// <summary>Average latency across connected samples, or null.</summary>
    public double? AvgLatencyMs { get; init; }

    /// <summary>Average packet loss across all samples.</summary>
    public double AvgPacketLossPercent { get; init; }

    /// <summary>Average jitter across samples that had jitter, or null.</summary>
    public double? AvgJitterMs { get; init; }

    /// <summary>Ordered findings, highest severity first.</summary>
    public required IReadOnlyList<DiagnosticFinding> Findings { get; init; }

    /// <summary>Suggested follow-up actions.</summary>
    public required IReadOnlyList<DiagnosticAction> SuggestedActions { get; init; }
}
