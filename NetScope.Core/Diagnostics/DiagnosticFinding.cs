namespace NetScope.Core.Diagnostics;

/// <summary>
/// A single observation produced by the diagnostic analyzer.
/// </summary>
public sealed class DiagnosticFinding
{
    /// <summary>Stable machine-readable code (e.g. <c>LOSS_TREND</c>).</summary>
    public required string Code { get; init; }

    /// <summary>Short title suitable for a list row.</summary>
    public required string Title { get; init; }

    /// <summary>Full sentence explaining the observation with measured values.</summary>
    public required string Detail { get; init; }

    /// <summary>Severity of this finding alone.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Metric or subsystem this finding belongs to.</summary>
    public required DiagnosticCategory Category { get; init; }
}
