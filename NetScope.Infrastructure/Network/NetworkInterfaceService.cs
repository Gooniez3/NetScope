using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Reads local network interface data from .NET's <see cref="NetworkInterface"/> API.
/// </summary>
public sealed class NetworkInterfaceService : INetworkInterfaceService
{
    public IReadOnlyList<NetworkInterfaceInfo> GetAllInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic =>
                nic.NetworkInterfaceType is not (
                    NetworkInterfaceType.Loopback or
                    NetworkInterfaceType.Tunnel) &&
                !IsFilterDriver(nic))
            .Select(MapInterface)
            .ToList();
    }

    public NetworkInterfaceInfo? GetActiveInterface()
    {
        return GetAllInterfaces()
            .FirstOrDefault(i =>
                i.IsUp &&
                i.GatewayAddresses.Count > 0 &&
                i.InterfaceType is
                    NetworkInterfaceType.Ethernet or
                    NetworkInterfaceType.Wireless80211);
    }

    private static NetworkInterfaceInfo MapInterface(NetworkInterface nic)
    {
        var ipProps = nic.GetIPProperties();
        var physicalAddress = nic.GetPhysicalAddress();

        return new NetworkInterfaceInfo
        {
            Id = nic.Id,
            Name = nic.Name,
            Description = nic.Description,
            InterfaceType = nic.NetworkInterfaceType,
            Status = nic.OperationalStatus,
            SpeedBitsPerSecond = nic.Speed,
            MacAddress = FormatMac(physicalAddress),

            IPv4Addresses = ipProps.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .ToList(),

            IPv6Addresses = ipProps.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6)
                .Select(a => a.Address.ToString())
                .ToList(),

            GatewayAddresses = ipProps.GatewayAddresses
                .Select(g => g.Address.ToString())
                .ToList(),

            DnsAddresses = ipProps.DnsAddresses
                .Select(d => d.ToString())
                .ToList(),

            DhcpServer = GetDhcpServer(ipProps),
            IsDhcpEnabled = GetDhcpServer(ipProps) is not null
        };
    }

    /// <summary>
    /// Filters out WFP/QoS/filter-driver sub-interfaces that clutter the list.
    /// These are virtual adapter layers Windows exposes but aren't real NICs.
    /// </summary>
    private static bool IsFilterDriver(NetworkInterface nic)
    {
        var desc = nic.Description;
        return desc.Contains("WFP", StringComparison.OrdinalIgnoreCase) ||
               desc.Contains("QoS Packet Scheduler", StringComparison.OrdinalIgnoreCase) ||
               desc.Contains("LightWeight Filter", StringComparison.OrdinalIgnoreCase) ||
               desc.Contains("Native WiFi Filter", StringComparison.OrdinalIgnoreCase) ||
               desc.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase) ||
               desc.Contains("Kernel Debug", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDhcpServer(System.Net.NetworkInformation.IPInterfaceProperties ipProps)
    {
        if (!OperatingSystem.IsMacOS())
        {
            try { return ipProps.DhcpServerAddresses.FirstOrDefault()?.ToString(); }
            catch { return null; }
        }
        return null;
    }

    private static string? FormatMac(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 0) return null;
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
}
