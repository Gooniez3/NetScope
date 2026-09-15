namespace NetScope.Core.Models;

/// <summary>
/// A complete point-in-time snapshot of network state — the data model
/// behind the Dashboard view.
/// </summary>
public sealed class NetworkSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }
    public required ConnectionStatus ConnectionStatus { get; init; }
    public required IReadOnlyList<NetworkInterfaceInfo> Interfaces { get; init; }
    public PublicIpInfo? PublicIp { get; init; }

    /// <summary>The interface most likely carrying default internet traffic.</summary>
    public NetworkInterfaceInfo? ActiveInterface =>
        Interfaces.FirstOrDefault(i =>
            i.IsUp &&
            i.GatewayAddresses.Count > 0 &&
            i.InterfaceType is
                System.Net.NetworkInformation.NetworkInterfaceType.Ethernet or
                System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211);
}
