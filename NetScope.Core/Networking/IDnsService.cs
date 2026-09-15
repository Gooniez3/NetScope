using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Performs DNS hostname resolution and returns structured results.
/// </summary>
/// <remarks>
/// Implementations must not throw for expected DNS failures (unknown host,
/// timeout). Those are represented as a <see cref="DnsQueryResult"/> with
/// <c>Success = false</c>. Exceptions are reserved for programming errors.
/// </remarks>
public interface IDnsService
{
    /// <summary>
    /// Resolves a hostname to its IP addresses.
    /// </summary>
    /// <param name="hostname">Hostname or IP address to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured resolution result including all resolved addresses.</returns>
    Task<DnsQueryResult> ResolveAsync(string hostname, CancellationToken cancellationToken = default);
}
