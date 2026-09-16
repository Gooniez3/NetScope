namespace NetScope.Core.Diagnostics;

/// <summary>
/// Groups a finding by the metric or subsystem it describes.
/// </summary>
public enum DiagnosticCategory
{
    /// <summary>Sample size, coverage, or missing data.</summary>
    Data,

    /// <summary>Reachability and disconnection patterns.</summary>
    Connectivity,

    /// <summary>Packet loss level or trend.</summary>
    PacketLoss,

    /// <summary>Round-trip latency level or trend.</summary>
    Latency,

    /// <summary>Jitter level or trend.</summary>
    Jitter,

    /// <summary>DNS resolution time.</summary>
    Dns,

    /// <summary>Local network versus upstream/destination path.</summary>
    Locality
}
