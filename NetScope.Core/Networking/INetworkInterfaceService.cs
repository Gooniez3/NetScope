using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Discovers and reports on local network interfaces.
/// </summary>
public interface INetworkInterfaceService
{
    /// <summary>
    /// Returns information about all network interfaces on the machine.
    /// </summary>
    IReadOnlyList<NetworkInterfaceInfo> GetAllInterfaces();

    /// <summary>
    /// Returns the interface most likely carrying default internet traffic,
    /// or null if none is found.
    /// </summary>
    NetworkInterfaceInfo? GetActiveInterface();
}
