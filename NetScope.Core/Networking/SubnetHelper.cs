using System.Net;
using System.Net.Sockets;

namespace NetScope.Core.Networking;

/// <summary>
/// Pure utility methods for IPv4 subnet calculations.
/// No OS or network I/O — fully unit-testable.
/// </summary>
public static class SubnetHelper
{
    /// <summary>Largest host list <see cref="GetHostAddresses"/> will allocate (/16).</summary>
    public const int MaxHostAddresses = 65_534;

    /// <summary>
    /// Parses a CIDR notation string (e.g. "192.168.1.0/24") into a network address and prefix length.
    /// </summary>
    public static (IPAddress Network, int PrefixLength) ParseCidr(string cidr)
    {
        ArgumentNullException.ThrowIfNull(cidr);

        var parts = cidr.Split('/');
        if (parts.Length != 2)
            throw new FormatException($"Invalid CIDR notation: '{cidr}'. Expected format: '192.168.1.0/24'.");

        if (!IPAddress.TryParse(parts[0], out var address) || address.AddressFamily != AddressFamily.InterNetwork)
            throw new FormatException($"Invalid IPv4 address in CIDR: '{parts[0]}'.");

        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32)
            throw new FormatException($"Invalid prefix length: '{parts[1]}'. Must be 0–32.");

        return (address, prefix);
    }

    /// <summary>
    /// Computes the subnet mask from a prefix length (e.g. 24 → 255.255.255.0).
    /// </summary>
    public static IPAddress PrefixToMask(int prefixLength)
    {
        if (prefixLength < 0 || prefixLength > 32)
            throw new ArgumentOutOfRangeException(nameof(prefixLength), "Must be 0–32.");

        var maskValue = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return new IPAddress(ToNetworkOrder(maskValue));
    }

    /// <summary>
    /// Computes the network address from an IP and prefix length.
    /// </summary>
    public static IPAddress GetNetworkAddress(IPAddress address, int prefixLength)
    {
        var ipBytes = address.GetAddressBytes();
        var maskBytes = PrefixToMask(prefixLength).GetAddressBytes();
        var networkBytes = new byte[4];
        for (var i = 0; i < 4; i++)
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        return new IPAddress(networkBytes);
    }

    /// <summary>
    /// Computes the broadcast address from a network address and prefix length.
    /// </summary>
    public static IPAddress GetBroadcastAddress(IPAddress networkAddress, int prefixLength)
    {
        var netBytes = networkAddress.GetAddressBytes();
        var maskBytes = PrefixToMask(prefixLength).GetAddressBytes();
        var broadcastBytes = new byte[4];
        for (var i = 0; i < 4; i++)
            broadcastBytes[i] = (byte)(netBytes[i] | ~maskBytes[i]);
        return new IPAddress(broadcastBytes);
    }

    /// <summary>
    /// Enumerates all usable host addresses in a subnet (excludes network and broadcast addresses).
    /// For a /24, this returns .1 through .254.
    /// </summary>
    public static IReadOnlyList<IPAddress> GetHostAddresses(IPAddress networkAddress, int prefixLength)
    {
        if (prefixLength >= 31)
            return []; // /31 and /32 have no usable host range

        var netUint = ToHostOrder(networkAddress.GetAddressBytes());
        var broadUint = ToHostOrder(GetBroadcastAddress(networkAddress, prefixLength).GetAddressBytes());

        var count = (long)broadUint - netUint - 1;
        if (count <= 0) return [];
        if (count > MaxHostAddresses)
            throw new ArgumentOutOfRangeException(
                nameof(prefixLength),
                $"Subnet has {count} hosts; maximum is {MaxHostAddresses} (/16 or narrower).");

        var addresses = new List<IPAddress>((int)count);
        for (var i = netUint + 1; i < broadUint; i++)
        {
            addresses.Add(new IPAddress(ToNetworkOrder(i)));
        }
        return addresses;
    }

    /// <summary>
    /// Infers the subnet CIDR from an interface's IPv4 address using the subnet mask
    /// from the OS. Falls back to /24 if the mask is unavailable.
    /// </summary>
    public static (IPAddress Network, int PrefixLength)? InferSubnetFromInterface(
        string ipv4Address, IReadOnlyList<(string Address, int PrefixLength)> unicastInfo)
    {
        if (!IPAddress.TryParse(ipv4Address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return null;

        var match = unicastInfo.FirstOrDefault(u => u.Address == ipv4Address);
        var prefix = match.PrefixLength > 0 ? match.PrefixLength : 24;

        var network = GetNetworkAddress(ip, prefix);
        return (network, prefix);
    }

    /// <summary>
    /// Returns CIDR string representation like "192.168.1.0/24".
    /// </summary>
    public static string ToCidrString(IPAddress network, int prefixLength)
        => $"{network}/{prefixLength}";

    private static uint ToHostOrder(byte[] bytes)
        => (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);

    private static byte[] ToNetworkOrder(uint value)
        => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
