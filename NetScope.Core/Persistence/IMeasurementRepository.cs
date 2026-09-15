using NetScope.Core.Models;

namespace NetScope.Core.Persistence;

/// <summary>
/// Repository for persisting and querying monitoring measurements and sessions.
/// </summary>
/// <remarks>
/// Implementations own database lifecycle (creation, connection, disposal).
/// Callers must dispose via <see cref="IAsyncDisposable"/> when finished.
/// </remarks>
public interface IMeasurementRepository : IAsyncDisposable
{
    /// <summary>
    /// Ensures the database schema exists. Safe to call multiple times.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    // --- Sessions ---

    /// <summary>Creates a new monitoring session and returns it with its assigned ID.</summary>
    Task<MonitoringSession> CreateSessionAsync(MonitoringSession session, CancellationToken ct = default);

    /// <summary>Updates session end time, measurement count, and completion status.</summary>
    Task CompleteSessionAsync(long sessionId, DateTimeOffset endedAt, int measurementCount, bool completedNormally, CancellationToken ct = default);

    /// <summary>Returns recent sessions ordered by start time descending.</summary>
    Task<IReadOnlyList<MonitoringSession>> GetRecentSessionsAsync(int limit = 10, CancellationToken ct = default);

    // --- Measurements ---

    /// <summary>Saves a single measurement associated with a session.</summary>
    Task SaveMeasurementAsync(long sessionId, NetworkMeasurement measurement, CancellationToken ct = default);

    /// <summary>Returns recent measurements across all sessions, ordered by timestamp descending.</summary>
    Task<IReadOnlyList<NetworkMeasurement>> GetRecentMeasurementsAsync(int limit = 50, CancellationToken ct = default);

    /// <summary>Returns measurements belonging to a specific session, ordered by cycle number.</summary>
    Task<IReadOnlyList<NetworkMeasurement>> GetMeasurementsBySessionAsync(long sessionId, CancellationToken ct = default);

    /// <summary>Returns measurements within the given UTC time range, ordered by timestamp.</summary>
    Task<IReadOnlyList<NetworkMeasurement>> GetMeasurementsByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    // --- Aggregates ---

    /// <summary>
    /// Computes aggregate statistics for a specific session.
    /// Returns null if the session has no measurements.
    /// </summary>
    Task<MeasurementAggregate?> GetSessionAggregateAsync(long sessionId, CancellationToken ct = default);

    /// <summary>
    /// Computes aggregate statistics for measurements within a time range.
    /// Returns null if no measurements exist in the range.
    /// </summary>
    Task<MeasurementAggregate?> GetTimeRangeAggregateAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    // --- Retention ---

    /// <summary>Deletes measurements older than the specified cutoff. Returns number deleted.</summary>
    Task<int> DeleteMeasurementsOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>Deletes sessions with no remaining measurements. Returns number deleted.</summary>
    Task<int> DeleteEmptySessionsAsync(CancellationToken ct = default);
}
