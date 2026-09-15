using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Performs multi-probe ICMP ping sessions and returns structured results with statistics.
/// </summary>
/// <remarks>
/// <para>Designed for reuse by the dashboard, live monitoring, CLI, historical metrics,
/// alerts, and AI diagnostics engine.</para>
/// <para>The <paramref name="progress"/> callback in <see cref="PingAsync"/> allows callers
/// to display results in real time (e.g. CLI output per probe, live chart updates)
/// without waiting for the full session to complete.</para>
/// <para>Implementations must not throw for expected network failures (timeout, unreachable
/// host, TTL expired). Those are represented as non-success <see cref="PingResult"/> entries.
/// Exceptions are reserved for programming errors and truly unexpected failures.</para>
/// </remarks>
public interface IPingService
{
    /// <summary>
    /// Executes a full ping session according to the given options.
    /// </summary>
    /// <param name="options">
    /// Session configuration. Will be validated before the session starts;
    /// invalid options throw <see cref="ArgumentException"/>.
    /// </param>
    /// <param name="progress">
    /// Optional callback invoked after each individual probe completes.
    /// Safe to pass <c>null</c> if real-time reporting is not needed.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token. When cancelled mid-session, the method returns the results
    /// collected so far rather than throwing.
    /// </param>
    /// <returns>
    /// The complete list of probe results and computed aggregate statistics.
    /// If cancelled early, statistics are computed over the probes that did complete.
    /// </returns>
    Task<(IReadOnlyList<PingResult> Results, PingStatistics Statistics)> PingAsync(
        PingOptions options,
        IProgress<PingResult>? progress = null,
        CancellationToken cancellationToken = default);
}
