using NetScope.Core.Networking;

namespace NetScope.Core.Models;

/// <summary>
/// Configuration for a TCP connect test. Validated via <see cref="Validate"/> before a probe.
/// </summary>
public sealed class PortTestOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 443;
    public int TimeoutMs { get; init; } = 3000;

    public const int MinPort = 1;
    public const int MaxPort = 65535;
    public const int MinTimeoutMs = 100;
    public const int MaxTimeoutMs = 30_000;

    public static readonly IReadOnlyDictionary<string, int> Presets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["http"] = 80,
        ["https"] = 443,
        ["ssh"] = 22,
        ["dns"] = 53,
        ["smtp"] = 25,
        ["rdp"] = 3389
    };

    public void Validate()
    {
        HostTarget.Validate(Host, nameof(Host));

        if (Port < MinPort || Port > MaxPort)
            throw new ArgumentException(
                $"{nameof(Port)} must be between {MinPort} and {MaxPort}, but was {Port}.", nameof(Port));

        if (TimeoutMs < MinTimeoutMs || TimeoutMs > MaxTimeoutMs)
            throw new ArgumentException(
                $"{nameof(TimeoutMs)} must be between {MinTimeoutMs} and {MaxTimeoutMs}, but was {TimeoutMs}.",
                nameof(TimeoutMs));
    }

    public static bool TryResolvePreset(string? name, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return Presets.TryGetValue(name.Trim(), out port);
    }
}
