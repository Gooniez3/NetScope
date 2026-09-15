namespace NetScope.Core.Models;

/// <summary>
/// Configuration for a traceroute session.
/// </summary>
public sealed class TracerouteOptions
{
    /// <summary>Target hostname or IP address to trace.</summary>
    public required string Target { get; init; }

    /// <summary>Maximum number of hops before giving up. Default 30.</summary>
    public int MaxHops { get; init; } = 30;

    /// <summary>Timeout in milliseconds for each hop probe. Default 3000.</summary>
    public int TimeoutMs { get; init; } = 3000;

    /// <summary>Whether to attempt reverse-DNS lookup on each hop. Default true.</summary>
    public bool ResolveHostnames { get; init; } = true;

    // --- Safe bounds ---

    public const int MinMaxHops = 1;
    public const int MaxMaxHops = 64;
    public const int MinTimeoutMs = 100;
    public const int MaxTimeoutMs = 10_000;

    /// <summary>
    /// Validates all options and throws <see cref="ArgumentException"/> if any value
    /// falls outside the allowed bounds.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Target))
            throw new ArgumentException("Target must not be empty.", nameof(Target));

        if (MaxHops < MinMaxHops || MaxHops > MaxMaxHops)
            throw new ArgumentException(
                $"MaxHops must be between {MinMaxHops} and {MaxMaxHops}, but was {MaxHops}.", nameof(MaxHops));

        if (TimeoutMs < MinTimeoutMs || TimeoutMs > MaxTimeoutMs)
            throw new ArgumentException(
                $"TimeoutMs must be between {MinTimeoutMs} and {MaxTimeoutMs}, but was {TimeoutMs}.", nameof(TimeoutMs));
    }
}
