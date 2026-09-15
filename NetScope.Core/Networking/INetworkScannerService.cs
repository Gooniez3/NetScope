using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Discovers devices on the local network via ping sweep.
/// </summary>
/// <remarks>
/// <para>Implementations must not throw for individual device probe failures.
/// Devices that do not respond are simply omitted from the result.</para>
/// <para>The <paramref name="progress"/> callback in <see cref="ScanAsync"/> allows callers
/// to display discovered devices in real time.</para>
/// </remarks>
public interface INetworkScannerService
{
    /// <summary>
    /// Scans a subnet for active devices.
    /// </summary>
    /// <param name="options">Scan configuration. Validated before the scan starts.</param>
    /// <param name="progress">Optional callback invoked when a device is discovered.</param>
    /// <param name="cancellationToken">
    /// Cancellation token. When cancelled, returns devices discovered so far
    /// with <see cref="NetworkScanResult.WasCancelled"/> set to true.
    /// </param>
    Task<NetworkScanResult> ScanAsync(
        ScanOptions options,
        IProgress<DiscoveredDevice>? progress = null,
        CancellationToken cancellationToken = default);
}
