using System.Net.Sockets;

namespace NetScope.Core.Models;

/// <summary>
/// Represents the outcome of a DNS resolution query for a single hostname.
/// </summary>
public sealed class DnsQueryResult
{
    /// <summary>The hostname that was queried.</summary>
    public required string Hostname { get; init; }

    /// <summary>Whether the resolution succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Resolved addresses with their address families.</summary>
    public IReadOnlyList<DnsResolvedAddress> Addresses { get; init; } = [];

    /// <summary>Time taken to resolve, in milliseconds.</summary>
    public required double ResolutionMs { get; init; }

    /// <summary>UTC timestamp when the query was performed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Error message when resolution failed, or null on success.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// A single resolved address with its address family.
/// </summary>
public sealed class DnsResolvedAddress
{
    /// <summary>The resolved IP address as a string.</summary>
    public required string Address { get; init; }

    /// <summary>The address family (InterNetwork for IPv4, InterNetworkV6 for IPv6).</summary>
    public required AddressFamily Family { get; init; }

    /// <summary>Human-readable family label.</summary>
    public string FamilyLabel => Family switch
    {
        AddressFamily.InterNetwork => "IPv4",
        AddressFamily.InterNetworkV6 => "IPv6",
        _ => Family.ToString()
    };
}
