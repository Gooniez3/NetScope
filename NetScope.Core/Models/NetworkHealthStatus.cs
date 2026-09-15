namespace NetScope.Core.Models;

/// <summary>
/// Categorizes current network health based on measurable indicators.
/// </summary>
/// <remarks>
/// <para><b>Classification thresholds (defaults):</b></para>
/// <list type="table">
///   <listheader><term>Status</term><description>Criteria</description></listheader>
///   <item><term>Healthy</term><description>Loss = 0%, Avg RTT &lt; 100 ms, Jitter &lt; 10 ms</description></item>
///   <item><term>Degraded</term><description>Loss &lt; 10%, or Avg RTT 100–500 ms, or Jitter 10–50 ms</description></item>
///   <item><term>Unstable</term><description>Loss 10–99%, or Avg RTT &gt; 500 ms, or Jitter &gt; 50 ms</description></item>
///   <item><term>Disconnected</term><description>Loss = 100% (no probes succeeded)</description></item>
/// </list>
/// <para>These thresholds are practical defaults, not universal network standards.
/// They are suitable for general internet connectivity monitoring. Future versions
/// may make them configurable.</para>
/// </remarks>
public enum NetworkHealthStatus
{
    /// <summary>All probes succeeded, low latency, low jitter.</summary>
    Healthy,

    /// <summary>Minor packet loss, elevated latency, or moderate jitter.</summary>
    Degraded,

    /// <summary>Significant packet loss, high latency, or high jitter.</summary>
    Unstable,

    /// <summary>No probes succeeded — connection appears down.</summary>
    Disconnected
}

/// <summary>
/// Pure classification logic for <see cref="NetworkHealthStatus"/>.
/// Deterministic, no I/O, fully unit-testable.
/// </summary>
public static class HealthClassifier
{
    /// <summary>
    /// Classifies network health from measurement metrics.
    /// </summary>
    /// <param name="packetLossPercent">Packet loss percentage (0–100).</param>
    /// <param name="avgRoundTripMs">Average RTT in milliseconds, or null if all probes failed.</param>
    /// <param name="jitterMs">Jitter in milliseconds, or null if fewer than 2 probes succeeded.</param>
    public static NetworkHealthStatus Classify(
        double packetLossPercent,
        double? avgRoundTripMs,
        double? jitterMs)
    {
        // 100% loss = disconnected
        if (packetLossPercent >= 100.0)
            return NetworkHealthStatus.Disconnected;

        // Check for Unstable first (worst non-disconnected state)
        if (packetLossPercent >= 10.0)
            return NetworkHealthStatus.Unstable;
        if (avgRoundTripMs.HasValue && avgRoundTripMs.Value > 500.0)
            return NetworkHealthStatus.Unstable;
        if (jitterMs.HasValue && jitterMs.Value > 50.0)
            return NetworkHealthStatus.Unstable;

        // Check for Degraded
        if (packetLossPercent > 0.0)
            return NetworkHealthStatus.Degraded;
        if (avgRoundTripMs.HasValue && avgRoundTripMs.Value >= 100.0)
            return NetworkHealthStatus.Degraded;
        if (jitterMs.HasValue && jitterMs.Value >= 10.0)
            return NetworkHealthStatus.Degraded;

        return NetworkHealthStatus.Healthy;
    }
}
