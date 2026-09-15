namespace NetScope.Core.Models;

/// <summary>
/// Represents a monitoring session — a series of measurements against a target.
/// </summary>
public sealed class MonitoringSession
{
    /// <summary>Unique session identifier.</summary>
    public required long Id { get; set; }

    /// <summary>UTC timestamp when the session started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>UTC timestamp when the session ended, or null if still running / interrupted.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Target that was monitored.</summary>
    public required string Target { get; init; }

    /// <summary>Configured interval in seconds.</summary>
    public required int IntervalSeconds { get; init; }

    /// <summary>Configured probes per measurement cycle.</summary>
    public required int ProbesPerMeasurement { get; init; }

    /// <summary>Configured timeout in milliseconds.</summary>
    public required int TimeoutMs { get; init; }

    /// <summary>Total number of measurements recorded in this session.</summary>
    public int MeasurementCount { get; set; }

    /// <summary>Whether the session completed normally (vs. interrupted/cancelled).</summary>
    public bool CompletedNormally { get; set; }
}
