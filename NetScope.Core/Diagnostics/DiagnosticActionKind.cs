namespace NetScope.Core.Diagnostics;

/// <summary>
/// Suggested next step a caller can offer after analysis.
/// </summary>
public enum DiagnosticActionKind
{
    /// <summary>No automated action — informational suggestion only.</summary>
    None,

    /// <summary>Open ping, DNS, and traceroute against the analyzed target.</summary>
    RunDiagnostics,

    /// <summary>Inspect the local adapter, Wi-Fi/cable, and default gateway.</summary>
    CheckLocalNetwork,

    /// <summary>Review historical sessions for a longer trend.</summary>
    ReviewHistory
}
