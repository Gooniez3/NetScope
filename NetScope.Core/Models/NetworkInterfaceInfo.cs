using System.Net.NetworkInformation;

namespace NetScope.Core.Models;

/// <summary>
/// Represents a snapshot of a single network interface and its addresses.
/// </summary>
public sealed class NetworkInterfaceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required NetworkInterfaceType InterfaceType { get; init; }
    public required OperationalStatus Status { get; init; }
    public required long SpeedBitsPerSecond { get; init; }
    public string? MacAddress { get; init; }

    public IReadOnlyList<string> IPv4Addresses { get; init; } = [];
    public IReadOnlyList<string> IPv6Addresses { get; init; } = [];

    /// <summary>IPv4 unicast addresses with their prefix lengths (for subnet calculation).</summary>
    public IReadOnlyList<(string Address, int PrefixLength)> IPv4UnicastDetails { get; init; } = [];
    public IReadOnlyList<string> GatewayAddresses { get; init; } = [];
    public IReadOnlyList<string> DnsAddresses { get; init; } = [];

    public string? DhcpServer { get; init; }
    public bool IsDhcpEnabled { get; init; }

    public bool IsUp => Status == OperationalStatus.Up;

    public string SpeedDisplay => SpeedBitsPerSecond switch
    {
        >= 1_000_000_000 => $"{SpeedBitsPerSecond / 1_000_000_000.0:F1} Gbps",
        >= 1_000_000 => $"{SpeedBitsPerSecond / 1_000_000.0:F0} Mbps",
        >= 1_000 => $"{SpeedBitsPerSecond / 1_000.0:F0} Kbps",
        _ => $"{SpeedBitsPerSecond} bps"
    };
}
