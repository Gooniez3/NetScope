namespace NetScope.Core.Models;

/// <summary>
/// Represents the outcome of a single ICMP echo request/reply probe.
/// </summary>
public sealed class PingResult
{
    /// <summary>1-based sequence number within the ping session.</summary>
    public required int SequenceNumber { get; init; }

    /// <summary>UTC timestamp when this probe was sent.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Whether the probe received a successful reply.</summary>
    public required bool Success { get; init; }

    /// <summary>Round-trip time in milliseconds, or null if the probe failed.</summary>
    public double? RoundTripTimeMs { get; init; }

    /// <summary>
    /// ICMP status string (e.g. "Success", "TimedOut", "TtlExpired").
    /// Maps from <see cref="System.Net.NetworkInformation.IPStatus"/>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Human-readable error description when the probe did not succeed, or null.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>TTL value from the reply, if available.</summary>
    public int? ReplyTtl { get; init; }

    /// <summary>Size of the reply buffer in bytes, if available.</summary>
    public int? ReplyBufferSize { get; init; }
}
