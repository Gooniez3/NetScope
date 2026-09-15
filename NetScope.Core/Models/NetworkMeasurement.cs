namespace NetScope.Core.Models;

/// <summary>
/// A single monitoring measurement — one cycle of probes and optional DNS/gateway checks.
/// </summary>
public sealed class NetworkMeasurement
{
    /// <summary>UTC timestamp when this measurement was taken.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>1-based cycle number within the monitoring session.</summary>
    public required int CycleNumber { get; init; }

    /// <summary>Target that was probed.</summary>
    public required string Target { get; init; }

    /// <summary>Whether the target was reachable (at least one probe succeeded).</summary>
    public required bool IsConnected { get; init; }

    /// <summary>Number of probes sent in this cycle.</summary>
    public required int PacketsSent { get; init; }

    /// <summary>Number of probes that received a successful reply.</summary>
    public required int PacketsReceived { get; init; }

    /// <summary>Packet loss percentage (0–100).</summary>
    public required double PacketLossPercent { get; init; }

    /// <summary>Minimum round-trip time in ms, or null if all probes failed.</summary>
    public double? MinLatencyMs { get; init; }

    /// <summary>Maximum round-trip time in ms, or null if all probes failed.</summary>
    public double? MaxLatencyMs { get; init; }

    /// <summary>Average round-trip time in ms, or null if all probes failed.</summary>
    public double? AvgLatencyMs { get; init; }

    /// <summary>Jitter in ms, or null if fewer than 2 probes succeeded.</summary>
    public double? JitterMs { get; init; }

    /// <summary>DNS resolution time in ms, or null if DNS measurement was not enabled.</summary>
    public double? DnsResolutionMs { get; init; }

    /// <summary>Gateway round-trip time in ms, or null if gateway measurement was not enabled.</summary>
    public double? GatewayLatencyMs { get; init; }

    /// <summary>Classified health status for this measurement.</summary>
    public required NetworkHealthStatus HealthStatus { get; init; }
}
