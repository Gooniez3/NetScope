namespace NetScope.Core.Diagnostics;

/// <summary>
/// A recommended next step after analysis.
/// </summary>
public sealed class DiagnosticAction
{
    /// <summary>Action type so UI/CLI can bind a command when applicable.</summary>
    public required DiagnosticActionKind Kind { get; init; }

    /// <summary>Button or list label (e.g. "Run Diagnostics").</summary>
    public required string Label { get; init; }

    /// <summary>Why this action is suggested.</summary>
    public required string Reason { get; init; }
}
