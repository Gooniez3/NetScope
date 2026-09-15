namespace NetScope.Core.Models;

/// <summary>
/// Configuration for a continuous monitoring session.
/// </summary>
public sealed class MonitorOptions
{
    /// <summary>Target host or IP to monitor. Default "1.1.1.1".</summary>
    public string Target { get; init; } = "1.1.1.1";

    /// <summary>Seconds between measurement cycles. Default 5.</summary>
    public int IntervalSeconds { get; init; } = 5;

    /// <summary>Timeout in milliseconds for each ping probe. Default 3000.</summary>
    public int TimeoutMs { get; init; } = 3000;

    /// <summary>Number of ping probes per measurement cycle. Default 4.</summary>
    public int ProbesPerMeasurement { get; init; } = 4;

    /// <summary>
    /// Maximum number of measurement cycles, or null for indefinite monitoring.
    /// When set, the monitor stops after this many cycles.
    /// </summary>
    public int? MaxCycles { get; init; }

    /// <summary>Whether to include DNS resolution timing in each measurement. Default false.</summary>
    public bool MeasureDns { get; init; }

    /// <summary>Whether to include gateway latency in each measurement. Default false.</summary>
    public bool MeasureGateway { get; init; }

    /// <summary>Gateway address for gateway measurement. Auto-detected if null.</summary>
    public string? GatewayAddress { get; init; }

    // --- Safe bounds ---

    public const int MinIntervalSeconds = 1;
    public const int MaxIntervalSeconds = 300;
    public const int MinTimeoutMs = 100;
    public const int MaxTimeoutMs = 30_000;
    public const int MinProbes = 1;
    public const int MaxProbes = 20;
    public const int MinMaxCycles = 1;
    public const int MaxMaxCycles = 100_000;

    /// <summary>
    /// Validates all options and throws <see cref="ArgumentException"/> if any value
    /// falls outside the allowed bounds.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Target))
            throw new ArgumentException("Target must not be empty.", nameof(Target));

        ValidateRange(IntervalSeconds, MinIntervalSeconds, MaxIntervalSeconds, nameof(IntervalSeconds));
        ValidateRange(TimeoutMs, MinTimeoutMs, MaxTimeoutMs, nameof(TimeoutMs));
        ValidateRange(ProbesPerMeasurement, MinProbes, MaxProbes, nameof(ProbesPerMeasurement));

        if (MaxCycles.HasValue)
            ValidateRange(MaxCycles.Value, MinMaxCycles, MaxMaxCycles, nameof(MaxCycles));
    }

    private static void ValidateRange(int value, int min, int max, string name)
    {
        if (value < min || value > max)
            throw new ArgumentException(
                $"{name} must be between {min} and {max}, but was {value}.", name);
    }
}
