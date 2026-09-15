namespace NetScope.Core.Models;

/// <summary>
/// Represents a device discovered during a LAN scan.
/// </summary>
public sealed class DiscoveredDevice
{
    /// <summary>IPv4 address of the device.</summary>
    public required string IpAddress { get; init; }

    /// <summary>Hostname resolved via reverse DNS, or null.</summary>
    public string? Hostname { get; init; }

    /// <summary>MAC address in XX:XX:XX:XX:XX:XX format, or null if unavailable.</summary>
    public string? MacAddress { get; init; }

    /// <summary>Response time in milliseconds for the initial ping probe.</summary>
    public required double ResponseTimeMs { get; init; }

    /// <summary>How the device was discovered (e.g. "ICMP", "ARP").</summary>
    public required string DiscoveryMethod { get; init; }

    /// <summary>Device status (e.g. "Online").</summary>
    public required string Status { get; init; }
}
