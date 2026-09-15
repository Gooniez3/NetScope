using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Tests basic internet connectivity.
/// </summary>
public interface IConnectionTestService
{
    /// <summary>
    /// Probes a target to determine whether the machine can reach the internet.
    /// </summary>
    /// <param name="target">IP or hostname to probe. Defaults to a well-known target if null.</param>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ConnectionStatus> TestConnectionAsync(
        string? target = null,
        int timeoutMs = 3000,
        CancellationToken cancellationToken = default);
}
