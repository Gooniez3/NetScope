using NetScope.Core.Networking;

namespace NetScope.Core.Models;

/// <summary>
/// Configuration for a LAN scan session.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>
    /// Subnet in CIDR notation (e.g. "192.168.1.0/24").
    /// When null, the scanner auto-detects from the active network interface.
    /// </summary>
    public string? Subnet { get; init; }

    /// <summary>Timeout per ping probe in milliseconds. Default 500.</summary>
    public int TimeoutMs { get; init; } = 500;

    /// <summary>Maximum number of concurrent probes. Default 32.</summary>
    public int Concurrency { get; init; } = 32;

    /// <summary>Whether to attempt reverse DNS lookups on discovered devices. Default true.</summary>
    public bool ResolveHostnames { get; init; } = true;

    /// <summary>Whether to attempt MAC address discovery (platform-dependent). Default true.</summary>
    public bool ResolveMacAddresses { get; init; } = true;

    // --- Safe bounds ---

    public const int MinTimeoutMs = 100;
    public const int MaxTimeoutMs = 10_000;
    public const int MinConcurrency = 1;
    public const int MaxConcurrency = 256;

    /// <summary>
    /// Widest CIDR prefix allowed for an explicit scan (~65k hosts for /16).
    /// Wider ranges are rejected to avoid memory and network exhaustion.
    /// </summary>
    public const int MinPrefixLength = 16;

    /// <summary>
    /// Validates configurable options and throws <see cref="ArgumentException"/>
    /// if any value falls outside the allowed bounds.
    /// </summary>
    public void Validate()
    {
        if (TimeoutMs < MinTimeoutMs || TimeoutMs > MaxTimeoutMs)
            throw new ArgumentException(
                $"TimeoutMs must be between {MinTimeoutMs} and {MaxTimeoutMs}, but was {TimeoutMs}.",
                nameof(TimeoutMs));

        if (Concurrency < MinConcurrency || Concurrency > MaxConcurrency)
            throw new ArgumentException(
                $"Concurrency must be between {MinConcurrency} and {MaxConcurrency}, but was {Concurrency}.",
                nameof(Concurrency));

        if (Subnet is null)
            return;

        try
        {
            var (_, prefix) = SubnetHelper.ParseCidr(Subnet);
            if (prefix < MinPrefixLength)
                throw new ArgumentException(
                    $"Subnet prefix must be /{MinPrefixLength} or narrower (maximum ~65,534 hosts).",
                    nameof(Subnet));
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(ex.Message, nameof(Subnet), ex);
        }
    }
}
