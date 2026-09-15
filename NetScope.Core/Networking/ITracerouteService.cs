using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Performs a traceroute to a target and returns structured hop-by-hop results.
/// </summary>
/// <remarks>
/// <para>Implementations must not throw for expected network failures (timeout, unreachable).
/// Those are represented in <see cref="TracerouteHop"/> and <see cref="TracerouteResult"/>.</para>
/// <para>The <paramref name="progress"/> callback in <see cref="TraceAsync"/> allows callers
/// to display hops in real time without waiting for the full trace to complete.</para>
/// </remarks>
public interface ITracerouteService
{
    /// <summary>
    /// Traces the route to a target host.
    /// </summary>
    /// <param name="options">Traceroute configuration. Validated before the trace starts.</param>
    /// <param name="progress">
    /// Optional callback invoked after each hop completes. Safe to pass null.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token. When cancelled, the method returns hops collected so far
    /// with <see cref="TracerouteResult.WasCancelled"/> set to true.
    /// </param>
    Task<TracerouteResult> TraceAsync(
        TracerouteOptions options,
        IProgress<TracerouteHop>? progress = null,
        CancellationToken cancellationToken = default);
}
