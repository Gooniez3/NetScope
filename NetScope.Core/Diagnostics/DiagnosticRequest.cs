using NetScope.Core.Models;

namespace NetScope.Core.Diagnostics;

/// <summary>
/// Input to <see cref="IDiagnosticAnalyzer"/>. Measurements are required; traceroute is optional context.
/// </summary>
public sealed class DiagnosticRequest
{
    /// <summary>Monitoring samples to analyze. Order does not matter — the analyzer sorts by timestamp.</summary>
    public required IReadOnlyList<NetworkMeasurement> Measurements { get; init; }

    /// <summary>Optional traceroute used to refine local vs remote locality.</summary>
    public TracerouteResult? Traceroute { get; init; }
}
