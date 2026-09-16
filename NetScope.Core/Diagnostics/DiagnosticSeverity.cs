namespace NetScope.Core.Diagnostics;

/// <summary>
/// Overall severity of a diagnostic report or individual finding.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>No issue, or informational context only.</summary>
    Info,

    /// <summary>Degraded performance that should be reviewed.</summary>
    Warning,

    /// <summary>Clear instability or significant metric shift.</summary>
    Alert,

    /// <summary>Outage or severe loss of connectivity.</summary>
    Critical
}
