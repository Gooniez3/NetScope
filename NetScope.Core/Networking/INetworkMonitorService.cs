using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Runs continuous network monitoring, emitting structured measurements at a configured interval.
/// </summary>
/// <remarks>
/// <para>The monitor runs until the <paramref name="cancellationToken"/> is signalled
/// or the configured <see cref="MonitorOptions.MaxCycles"/> is reached.</para>
/// <para>If a measurement cycle takes longer than the configured interval,
/// the next cycle starts immediately without overlapping.</para>
/// <para>Individual cycle failures are represented as measurements with
/// <see cref="NetworkMeasurement.IsConnected"/> = false rather than thrown exceptions.</para>
/// </remarks>
public interface INetworkMonitorService
{
    /// <summary>
    /// Starts monitoring and yields measurements as they complete.
    /// </summary>
    /// <param name="options">Monitoring configuration. Validated before the first cycle.</param>
    /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
    /// <returns>Async stream of measurements, one per cycle.</returns>
    IAsyncEnumerable<NetworkMeasurement> MonitorAsync(
        MonitorOptions options,
        CancellationToken cancellationToken = default);
}
