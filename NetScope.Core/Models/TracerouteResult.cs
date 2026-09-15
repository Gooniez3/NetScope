namespace NetScope.Core.Models;

/// <summary>
/// Represents the complete result of a traceroute to a target.
/// </summary>
public sealed class TracerouteResult
{
    /// <summary>The target hostname or IP that was traced.</summary>
    public required string Target { get; init; }

    /// <summary>The resolved IP address of the target, or null if resolution failed.</summary>
    public string? ResolvedAddress { get; init; }

    /// <summary>Ordered collection of hops from source to destination.</summary>
    public required IReadOnlyList<TracerouteHop> Hops { get; init; }

    /// <summary>Whether the trace reached the final destination.</summary>
    public required bool DestinationReached { get; init; }

    /// <summary>Total duration of the traceroute in milliseconds.</summary>
    public required double TotalDurationMs { get; init; }

    /// <summary>UTC timestamp when the traceroute started.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Whether the traceroute was cancelled before completion.</summary>
    public bool WasCancelled { get; init; }

    /// <summary>Error message if the traceroute could not start, or null.</summary>
    public string? ErrorMessage { get; init; }
}
