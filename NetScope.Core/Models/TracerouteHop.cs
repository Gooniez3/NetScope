namespace NetScope.Core.Models;

/// <summary>
/// Represents a single hop in a traceroute path.
/// </summary>
public sealed class TracerouteHop
{
    /// <summary>1-based hop number (TTL value used).</summary>
    public required int HopNumber { get; init; }

    /// <summary>IP address of the responding node, or null if no response.</summary>
    public string? Address { get; init; }

    /// <summary>Reverse-DNS hostname of the responding node, or null.</summary>
    public string? Hostname { get; init; }

    /// <summary>Round-trip time in milliseconds, or null if the hop timed out.</summary>
    public double? RoundTripTimeMs { get; init; }

    /// <summary>Whether this hop responded within the timeout.</summary>
    public required bool Responded { get; init; }

    /// <summary>Status description (e.g. "Success", "TtlExpired", "TimedOut").</summary>
    public required string Status { get; init; }
}
