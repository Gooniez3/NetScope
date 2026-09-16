using NetScope.Core.Networking;

namespace NetScope.Core.Models;

/// <summary>
/// Configuration for a ping session. All values are validated via <see cref="Validate"/>
/// before a session begins. Bounds are chosen to prevent excessive network traffic.
/// </summary>
public sealed class PingOptions
{
    /// <summary>Target host or IP address to ping.</summary>
    public required string Target { get; init; }

    /// <summary>Number of ICMP echo requests to send. Default 4.</summary>
    public int Count { get; init; } = 4;

    /// <summary>Milliseconds between successive probes. Default 1000.</summary>
    public int IntervalMs { get; init; } = 1000;

    /// <summary>Milliseconds to wait for each reply before declaring timeout. Default 3000.</summary>
    public int TimeoutMs { get; init; } = 3000;

    /// <summary>ICMP payload size in bytes. Default 32.</summary>
    public int PayloadSize { get; init; } = 32;

    /// <summary>
    /// Time-to-live (hop limit). Null uses the OS default (typically 128 on Windows, 64 on Linux/macOS).
    /// Note: TTL support varies by platform — see cross-platform notes in README.
    /// </summary>
    public int? Ttl { get; init; }

    // --- Safe bounds ---

    public const int MinCount = 1;
    public const int MaxCount = 1000;
    public const int MinIntervalMs = 100;
    public const int MaxIntervalMs = 60_000;
    public const int MinTimeoutMs = 100;
    public const int MaxTimeoutMs = 30_000;
    public const int MinPayloadSize = 0;
    public const int MaxPayloadSize = 65_500;
    public const int MinTtl = 1;
    public const int MaxTtl = 255;

    /// <summary>
    /// Validates all options and throws <see cref="ArgumentException"/> if any value
    /// falls outside the allowed bounds.
    /// </summary>
    public void Validate()
    {
        HostTarget.Validate(Target);

        ValidateRange(Count, MinCount, MaxCount, nameof(Count));
        ValidateRange(IntervalMs, MinIntervalMs, MaxIntervalMs, nameof(IntervalMs));
        ValidateRange(TimeoutMs, MinTimeoutMs, MaxTimeoutMs, nameof(TimeoutMs));
        ValidateRange(PayloadSize, MinPayloadSize, MaxPayloadSize, nameof(PayloadSize));

        if (Ttl.HasValue)
            ValidateRange(Ttl.Value, MinTtl, MaxTtl, nameof(Ttl));
    }

    private static void ValidateRange(int value, int min, int max, string name)
    {
        if (value < min || value > max)
            throw new ArgumentException(
                $"{name} must be between {min} and {max}, but was {value}.", name);
    }
}
