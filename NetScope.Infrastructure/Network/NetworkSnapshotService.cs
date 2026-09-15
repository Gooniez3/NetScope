using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Composes a full <see cref="NetworkSnapshot"/> by calling each underlying service.
/// </summary>
public sealed class NetworkSnapshotService : INetworkSnapshotService
{
    private readonly INetworkInterfaceService _interfaceService;
    private readonly IConnectionTestService _connectionTestService;
    private readonly IPublicIpService _publicIpService;

    public NetworkSnapshotService(
        INetworkInterfaceService interfaceService,
        IConnectionTestService connectionTestService,
        IPublicIpService publicIpService)
    {
        _interfaceService = interfaceService;
        _connectionTestService = connectionTestService;
        _publicIpService = publicIpService;
    }

    public async Task<NetworkSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var interfaces = _interfaceService.GetAllInterfaces();

        // Run connection test and public IP lookup concurrently
        var connectionTask = _connectionTestService.TestConnectionAsync(
            cancellationToken: cancellationToken);
        var publicIpTask = _publicIpService.GetPublicIpAsync(cancellationToken);

        await Task.WhenAll(connectionTask, publicIpTask);

        return new NetworkSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Interfaces = interfaces,
            ConnectionStatus = await connectionTask,
            PublicIp = await publicIpTask
        };
    }
}
