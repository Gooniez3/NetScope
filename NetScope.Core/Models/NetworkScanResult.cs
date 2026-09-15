namespace NetScope.Core.Models;

/// <summary>
/// Complete result of a LAN scan session.
/// </summary>
public sealed class NetworkScanResult
{
    /// <summary>Scanned subnet in CIDR notation (e.g. "192.168.1.0/24").</summary>
    public required string ScannedRange { get; init; }

    /// <summary>UTC timestamp when the scan started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>UTC timestamp when the scan completed.</summary>
    public required DateTimeOffset CompletedAt { get; init; }

    /// <summary>Total scan duration.</summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>Number of host addresses that were probed.</summary>
    public required int AddressesScanned { get; init; }

    /// <summary>Number of devices that responded.</summary>
    public int DevicesDiscovered => Devices.Count;

    /// <summary>Devices that responded to the scan, ordered by IP address.</summary>
    public required IReadOnlyList<DiscoveredDevice> Devices { get; init; }

    /// <summary>Whether the scan was cancelled before completion.</summary>
    public bool WasCancelled { get; init; }
}
