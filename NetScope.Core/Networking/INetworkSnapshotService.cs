using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Composes a full network snapshot by aggregating data from individual services.
/// </summary>
public interface INetworkSnapshotService
{
    /// <summary>
    /// Gathers all available network information into a single snapshot.
    /// </summary>
    Task<NetworkSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default);
}
