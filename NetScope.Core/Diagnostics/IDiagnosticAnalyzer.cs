namespace NetScope.Core.Diagnostics;

/// <summary>
/// Explains monitoring measurements as structured findings and recommended actions.
/// </summary>
/// <remarks>
/// <para>
/// Implementations must be deterministic and free of I/O. The default engine is a
/// rule-based analyzer — not a remote language-model call — so results are testable,
/// offline, and suitable for reuse by the desktop UI, CLI, and future optional
/// API-based narration.
/// </para>
/// </remarks>
public interface IDiagnosticAnalyzer
{
    /// <summary>
    /// Analyzes the given measurements and optional traceroute context.
    /// Never throws for empty or incomplete data — those cases produce an Info report.
    /// </summary>
    DiagnosticReport Analyze(DiagnosticRequest request);
}
