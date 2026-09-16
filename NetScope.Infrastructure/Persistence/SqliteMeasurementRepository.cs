using Microsoft.Data.Sqlite;
using NetScope.Core.Models;
using NetScope.Core.Persistence;

namespace NetScope.Infrastructure.Persistence;

/// <summary>
/// SQLite implementation of <see cref="IMeasurementRepository"/>.
/// Uses raw ADO.NET with parameterized queries — no ORM overhead.
/// </summary>
/// <remarks>
/// <para>The database file is created automatically on first use via <see cref="InitializeAsync"/>.</para>
/// <para>Default location: <c>~/.netscope/netscope.db</c> (configurable via constructor).</para>
/// <para>WAL journal mode is enabled for better concurrent read performance.</para>
/// </remarks>
public sealed class SqliteMeasurementRepository : IMeasurementRepository
{
    private readonly SqliteConnection _connection;
    private readonly string _databasePath;
    private bool _disposed;

    /// <summary>
    /// Creates a repository backed by the specified SQLite database file.
    /// </summary>
    /// <param name="databasePath">
    /// Full path to the SQLite database file, or ":memory:" for in-memory databases (testing).
    /// If null, uses the default location <c>~/.netscope/netscope.db</c>.
    /// </param>
    public SqliteMeasurementRepository(string? databasePath = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        _databasePath = path;
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = path == ":memory:" ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 5
        }.ToString();

        _connection = new SqliteConnection(connectionString);
    }

    /// <summary>
    /// Gets the default database path: <c>~/.netscope/netscope.db</c>.
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".netscope");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "netscope.db");
    }

    private static void RestrictDatabasePermissions(string dbPath)
    {
        if (OperatingSystem.IsWindows() || dbPath == ":memory:" || !File.Exists(dbPath))
            return;

        try
        {
            File.SetUnixFileMode(
                dbPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best-effort; location under the user profile is already private on most systems.
        }
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            PRAGMA busy_timeout=5000;

            CREATE TABLE IF NOT EXISTS sessions (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at      TEXT    NOT NULL,
                ended_at        TEXT,
                target          TEXT    NOT NULL,
                interval_sec    INTEGER NOT NULL,
                probes          INTEGER NOT NULL,
                timeout_ms      INTEGER NOT NULL,
                measurement_cnt INTEGER NOT NULL DEFAULT 0,
                completed       INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS measurements (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id      INTEGER NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
                timestamp       TEXT    NOT NULL,
                cycle_number    INTEGER NOT NULL,
                target          TEXT    NOT NULL,
                is_connected    INTEGER NOT NULL,
                packets_sent    INTEGER NOT NULL,
                packets_recv    INTEGER NOT NULL,
                loss_pct        REAL    NOT NULL,
                min_latency     REAL,
                max_latency     REAL,
                avg_latency     REAL,
                jitter          REAL,
                dns_ms          REAL,
                gateway_ms      REAL,
                health_status   INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_measurements_session
                ON measurements(session_id);

            CREATE INDEX IF NOT EXISTS idx_measurements_timestamp
                ON measurements(timestamp);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        RestrictDatabasePermissions(_databasePath);
    }

    // --- Sessions ---

    public async Task<MonitoringSession> CreateSessionAsync(
        MonitoringSession session, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (started_at, target, interval_sec, probes, timeout_ms)
            VALUES ($started, $target, $interval, $probes, $timeout);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$started", session.StartedAt.UtcDateTime.ToString("o"));
        cmd.Parameters.AddWithValue("$target", session.Target);
        cmd.Parameters.AddWithValue("$interval", session.IntervalSeconds);
        cmd.Parameters.AddWithValue("$probes", session.ProbesPerMeasurement);
        cmd.Parameters.AddWithValue("$timeout", session.TimeoutMs);

        var id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        session.Id = id;
        return session;
    }

    public async Task CompleteSessionAsync(
        long sessionId, DateTimeOffset endedAt, int measurementCount,
        bool completedNormally, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE sessions
            SET ended_at = $ended, measurement_cnt = $cnt, completed = $completed
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", sessionId);
        cmd.Parameters.AddWithValue("$ended", endedAt.UtcDateTime.ToString("o"));
        cmd.Parameters.AddWithValue("$cnt", measurementCount);
        cmd.Parameters.AddWithValue("$completed", completedNormally ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<MonitoringSession>> GetRecentSessionsAsync(
        int limit = 10, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var take = ClampLimit(limit);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, started_at, ended_at, target, interval_sec, probes, timeout_ms,
                   measurement_cnt, completed
            FROM sessions ORDER BY started_at DESC LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", take);

        var sessions = new List<MonitoringSession>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            sessions.Add(ReadSession(reader));
        }
        return sessions;
    }

    // --- Measurements ---

    public async Task SaveMeasurementAsync(
        long sessionId, NetworkMeasurement measurement, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO measurements
                (session_id, timestamp, cycle_number, target, is_connected,
                 packets_sent, packets_recv, loss_pct,
                 min_latency, max_latency, avg_latency, jitter,
                 dns_ms, gateway_ms, health_status)
            VALUES
                ($sid, $ts, $cycle, $target, $connected,
                 $sent, $recv, $loss,
                 $min, $max, $avg, $jitter,
                 $dns, $gw, $health);
            """;
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$ts", measurement.Timestamp.UtcDateTime.ToString("o"));
        cmd.Parameters.AddWithValue("$cycle", measurement.CycleNumber);
        cmd.Parameters.AddWithValue("$target", measurement.Target);
        cmd.Parameters.AddWithValue("$connected", measurement.IsConnected ? 1 : 0);
        cmd.Parameters.AddWithValue("$sent", measurement.PacketsSent);
        cmd.Parameters.AddWithValue("$recv", measurement.PacketsReceived);
        cmd.Parameters.AddWithValue("$loss", measurement.PacketLossPercent);
        AddNullableDouble(cmd, "$min", measurement.MinLatencyMs);
        AddNullableDouble(cmd, "$max", measurement.MaxLatencyMs);
        AddNullableDouble(cmd, "$avg", measurement.AvgLatencyMs);
        AddNullableDouble(cmd, "$jitter", measurement.JitterMs);
        AddNullableDouble(cmd, "$dns", measurement.DnsResolutionMs);
        AddNullableDouble(cmd, "$gw", measurement.GatewayLatencyMs);
        cmd.Parameters.AddWithValue("$health", (int)measurement.HealthStatus);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<NetworkMeasurement>> GetRecentMeasurementsAsync(
        int limit = 50, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM measurements ORDER BY timestamp DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", ClampLimit(limit));

        return await ReadMeasurementsAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<NetworkMeasurement>> GetMeasurementsBySessionAsync(
        long sessionId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM measurements WHERE session_id = $sid ORDER BY cycle_number;";
        cmd.Parameters.AddWithValue("$sid", sessionId);

        return await ReadMeasurementsAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<NetworkMeasurement>> GetMeasurementsByTimeRangeAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM measurements
            WHERE timestamp >= $from AND timestamp <= $to
            ORDER BY timestamp;
            """;
        cmd.Parameters.AddWithValue("$from", from.UtcDateTime.ToString("o"));
        cmd.Parameters.AddWithValue("$to", to.UtcDateTime.ToString("o"));

        return await ReadMeasurementsAsync(cmd, ct);
    }

    // --- Aggregates ---

    public async Task<MeasurementAggregate?> GetSessionAggregateAsync(
        long sessionId, CancellationToken ct = default)
    {
        var measurements = await GetMeasurementsBySessionAsync(sessionId, ct);
        return ComputeAggregate(measurements);
    }

    public async Task<MeasurementAggregate?> GetTimeRangeAggregateAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var measurements = await GetMeasurementsByTimeRangeAsync(from, to, ct);
        return ComputeAggregate(measurements);
    }

    // --- Retention ---

    public async Task<int> DeleteMeasurementsOlderThanAsync(
        DateTimeOffset cutoff, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM measurements WHERE timestamp < $cutoff;";
        cmd.Parameters.AddWithValue("$cutoff", cutoff.UtcDateTime.ToString("o"));

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> DeleteEmptySessionsAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM sessions
            WHERE id NOT IN (SELECT DISTINCT session_id FROM measurements);
            """;
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> DeleteSessionAsync(long sessionId, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var tx = (SqliteTransaction)await _connection.BeginTransactionAsync(ct);

        await using var meas = _connection.CreateCommand();
        meas.Transaction = tx;
        meas.CommandText = "DELETE FROM measurements WHERE session_id = $id;";
        meas.Parameters.AddWithValue("$id", sessionId);
        var removed = await meas.ExecuteNonQueryAsync(ct);

        await using var sess = _connection.CreateCommand();
        sess.Transaction = tx;
        sess.CommandText = "DELETE FROM sessions WHERE id = $id;";
        sess.Parameters.AddWithValue("$id", sessionId);
        await sess.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return removed;
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        await using var tx = (SqliteTransaction)await _connection.BeginTransactionAsync(ct);

        await using var countCmd = _connection.CreateCommand();
        countCmd.Transaction = tx;
        countCmd.CommandText = "SELECT COUNT(*) FROM sessions;";
        var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var meas = _connection.CreateCommand();
        meas.Transaction = tx;
        meas.CommandText = "DELETE FROM measurements;";
        await meas.ExecuteNonQueryAsync(ct);

        await using var sess = _connection.CreateCommand();
        sess.Transaction = tx;
        sess.CommandText = "DELETE FROM sessions;";
        await sess.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return count;
    }

    // --- Disposal ---

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _connection.DisposeAsync();
    }

    // --- Helpers ---

    private async Task EnsureOpenAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
            await using var pragma = _connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
            await pragma.ExecuteNonQueryAsync(ct);
        }
    }

    private static int ClampLimit(int limit) => Math.Clamp(limit, 1, 10_000);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;

    private static NetworkHealthStatus ReadHealthStatus(int raw) =>
        Enum.IsDefined(typeof(NetworkHealthStatus), raw)
            ? (NetworkHealthStatus)raw
            : NetworkHealthStatus.Disconnected;

    private static void AddNullableDouble(SqliteCommand cmd, string name, double? value)
    {
        cmd.Parameters.AddWithValue(name, value.HasValue ? (object)value.Value : DBNull.Value);
    }

    private static double? ReadNullableDouble(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
    }

    private static MonitoringSession ReadSession(SqliteDataReader reader)
    {
        var startedAt = ParseTimestamp(reader.GetString(reader.GetOrdinal("started_at")));
        DateTimeOffset? endedAt = null;
        if (!reader.IsDBNull(reader.GetOrdinal("ended_at")))
            endedAt = ParseTimestamp(reader.GetString(reader.GetOrdinal("ended_at")));

        return new MonitoringSession
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            StartedAt = startedAt,
            EndedAt = endedAt,
            Target = reader.GetString(reader.GetOrdinal("target")),
            IntervalSeconds = reader.GetInt32(reader.GetOrdinal("interval_sec")),
            ProbesPerMeasurement = reader.GetInt32(reader.GetOrdinal("probes")),
            TimeoutMs = reader.GetInt32(reader.GetOrdinal("timeout_ms")),
            MeasurementCount = reader.GetInt32(reader.GetOrdinal("measurement_cnt")),
            CompletedNormally = reader.GetInt32(reader.GetOrdinal("completed")) == 1
        };
    }

    private static NetworkMeasurement ReadMeasurement(SqliteDataReader reader)
    {
        return new NetworkMeasurement
        {
            Timestamp = ParseTimestamp(reader.GetString(reader.GetOrdinal("timestamp"))),
            CycleNumber = reader.GetInt32(reader.GetOrdinal("cycle_number")),
            Target = reader.GetString(reader.GetOrdinal("target")),
            IsConnected = reader.GetInt32(reader.GetOrdinal("is_connected")) == 1,
            PacketsSent = reader.GetInt32(reader.GetOrdinal("packets_sent")),
            PacketsReceived = reader.GetInt32(reader.GetOrdinal("packets_recv")),
            PacketLossPercent = reader.GetDouble(reader.GetOrdinal("loss_pct")),
            MinLatencyMs = ReadNullableDouble(reader, "min_latency"),
            MaxLatencyMs = ReadNullableDouble(reader, "max_latency"),
            AvgLatencyMs = ReadNullableDouble(reader, "avg_latency"),
            JitterMs = ReadNullableDouble(reader, "jitter"),
            DnsResolutionMs = ReadNullableDouble(reader, "dns_ms"),
            GatewayLatencyMs = ReadNullableDouble(reader, "gateway_ms"),
            HealthStatus = ReadHealthStatus(reader.GetInt32(reader.GetOrdinal("health_status")))
        };
    }

    private static async Task<IReadOnlyList<NetworkMeasurement>> ReadMeasurementsAsync(
        SqliteCommand cmd, CancellationToken ct)
    {
        var measurements = new List<NetworkMeasurement>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            measurements.Add(ReadMeasurement(reader));
        }
        return measurements;
    }

    private static MeasurementAggregate? ComputeAggregate(IReadOnlyList<NetworkMeasurement> measurements)
    {
        if (measurements.Count == 0) return null;

        var connected = measurements.Where(m => m.IsConnected).ToList();
        var disconnected = measurements.Count - connected.Count;

        var avgLatencies = connected.Where(m => m.AvgLatencyMs.HasValue)
            .Select(m => m.AvgLatencyMs!.Value).ToList();
        var minLatencies = connected.Where(m => m.MinLatencyMs.HasValue)
            .Select(m => m.MinLatencyMs!.Value).ToList();
        var maxLatencies = connected.Where(m => m.MaxLatencyMs.HasValue)
            .Select(m => m.MaxLatencyMs!.Value).ToList();
        var jitters = measurements.Where(m => m.JitterMs.HasValue)
            .Select(m => m.JitterMs!.Value).ToList();

        var healthBreakdown = measurements
            .GroupBy(m => m.HealthStatus)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var status in Enum.GetValues<NetworkHealthStatus>())
            healthBreakdown.TryAdd(status, 0);

        return new MeasurementAggregate
        {
            TotalMeasurements = measurements.Count,
            ConnectedCount = connected.Count,
            DisconnectedCount = disconnected,
            UptimePercent = connected.Count / (double)measurements.Count * 100.0,
            AvgLatencyMs = avgLatencies.Count > 0 ? avgLatencies.Average() : null,
            MinLatencyMs = minLatencies.Count > 0 ? minLatencies.Min() : null,
            MaxLatencyMs = maxLatencies.Count > 0 ? maxLatencies.Max() : null,
            AvgJitterMs = jitters.Count > 0 ? jitters.Average() : null,
            AvgPacketLossPercent = measurements.Average(m => m.PacketLossPercent),
            HealthBreakdown = healthBreakdown,
            PeriodStart = measurements.Min(m => m.Timestamp),
            PeriodEnd = measurements.Max(m => m.Timestamp)
        };
    }
}
