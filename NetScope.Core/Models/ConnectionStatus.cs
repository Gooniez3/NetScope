namespace NetScope.Core.Models;

/// <summary>
/// Represents the current internet connection status including latency.
/// </summary>
public sealed class ConnectionStatus
{
    public required bool IsConnected { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Round-trip time in milliseconds to the test target, or null if unreachable.</summary>
    public double? LatencyMs { get; init; }

    /// <summary>The target that was probed (e.g. "1.1.1.1").</summary>
    public string? ProbeTarget { get; init; }

    /// <summary>Human-readable summary.</summary>
    public string Summary => IsConnected
        ? $"Connected ({LatencyMs:F1} ms to {ProbeTarget})"
        : "Disconnected";
}
