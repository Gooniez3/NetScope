namespace NetScope.Core.Models;

/// <summary>
/// Aggregate statistics computed from a set of historical measurements.
/// </summary>
public sealed class MeasurementAggregate
{
    /// <summary>Number of measurements in this aggregate.</summary>
    public required int TotalMeasurements { get; init; }

    /// <summary>Number of measurements where the target was reachable.</summary>
    public required int ConnectedCount { get; init; }

    /// <summary>Number of measurements where the target was unreachable.</summary>
    public required int DisconnectedCount { get; init; }

    /// <summary>Overall uptime percentage (0–100).</summary>
    public required double UptimePercent { get; init; }

    /// <summary>Average latency across all connected measurements, or null.</summary>
    public double? AvgLatencyMs { get; init; }

    /// <summary>Minimum latency across all connected measurements, or null.</summary>
    public double? MinLatencyMs { get; init; }

    /// <summary>Maximum latency across all connected measurements, or null.</summary>
    public double? MaxLatencyMs { get; init; }

    /// <summary>Average jitter across measurements that had jitter data, or null.</summary>
    public double? AvgJitterMs { get; init; }

    /// <summary>Average packet loss percentage across all measurements.</summary>
    public required double AvgPacketLossPercent { get; init; }

    /// <summary>Count of measurements per health status.</summary>
    public required IReadOnlyDictionary<NetworkHealthStatus, int> HealthBreakdown { get; init; }

    /// <summary>Start of the aggregated period.</summary>
    public DateTimeOffset? PeriodStart { get; init; }

    /// <summary>End of the aggregated period.</summary>
    public DateTimeOffset? PeriodEnd { get; init; }
}
