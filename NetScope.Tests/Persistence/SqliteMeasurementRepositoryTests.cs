using NetScope.Core.Models;
using NetScope.Infrastructure.Persistence;

namespace NetScope.Tests.Persistence;

/// <summary>
/// Tests for <see cref="SqliteMeasurementRepository"/> using in-memory SQLite.
/// Each test gets a fresh, isolated database instance.
/// </summary>
public class SqliteMeasurementRepositoryTests : IAsyncDisposable
{
    private readonly SqliteMeasurementRepository _repo;

    public SqliteMeasurementRepositoryTests()
    {
        _repo = new SqliteMeasurementRepository(":memory:");
    }

    public async ValueTask DisposeAsync()
    {
        await _repo.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    // --- Helper factories ---

    private static MonitoringSession MakeSession(string target = "1.1.1.1") => new()
    {
        Id = 0,
        StartedAt = DateTimeOffset.UtcNow,
        Target = target,
        IntervalSeconds = 5,
        ProbesPerMeasurement = 4,
        TimeoutMs = 3000
    };

    private static NetworkMeasurement MakeHealthy(int cycle = 1, string target = "1.1.1.1") => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        CycleNumber = cycle,
        Target = target,
        IsConnected = true,
        PacketsSent = 4,
        PacketsReceived = 4,
        PacketLossPercent = 0.0,
        MinLatencyMs = 3.0,
        MaxLatencyMs = 8.0,
        AvgLatencyMs = 5.0,
        JitterMs = 1.2,
        HealthStatus = NetworkHealthStatus.Healthy
    };

    private static NetworkMeasurement MakeDisconnected(int cycle = 1) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        CycleNumber = cycle,
        Target = "1.1.1.1",
        IsConnected = false,
        PacketsSent = 4,
        PacketsReceived = 0,
        PacketLossPercent = 100.0,
        HealthStatus = NetworkHealthStatus.Disconnected
    };

    // ===============================
    // Database initialization
    // ===============================

    [Fact]
    public async Task InitializeAsync_CreatesSchema_NoException()
    {
        await _repo.InitializeAsync();
    }

    [Fact]
    public async Task InitializeAsync_IdempotentCallsTwice_NoException()
    {
        await _repo.InitializeAsync();
        await _repo.InitializeAsync();
    }

    // ===============================
    // Sessions
    // ===============================

    [Fact]
    public async Task CreateSessionAsync_ReturnsWithAssignedId()
    {
        await _repo.InitializeAsync();

        var session = MakeSession();
        var created = await _repo.CreateSessionAsync(session);

        Assert.True(created.Id > 0);
        Assert.Equal("1.1.1.1", created.Target);
    }

    [Fact]
    public async Task CreateSessionAsync_MultipleSessions_UniqueIds()
    {
        await _repo.InitializeAsync();

        var s1 = await _repo.CreateSessionAsync(MakeSession());
        var s2 = await _repo.CreateSessionAsync(MakeSession("8.8.8.8"));

        Assert.NotEqual(s1.Id, s2.Id);
    }

    [Fact]
    public async Task CompleteSessionAsync_UpdatesFields()
    {
        await _repo.InitializeAsync();

        var session = await _repo.CreateSessionAsync(MakeSession());
        var endTime = DateTimeOffset.UtcNow.AddMinutes(5);

        await _repo.CompleteSessionAsync(session.Id, endTime, 10, true);

        var sessions = await _repo.GetRecentSessionsAsync(1);
        Assert.Single(sessions);
        Assert.NotNull(sessions[0].EndedAt);
        Assert.Equal(10, sessions[0].MeasurementCount);
        Assert.True(sessions[0].CompletedNormally);
    }

    [Fact]
    public async Task CompleteSessionAsync_InterruptedSession_CompletedNormallyFalse()
    {
        await _repo.InitializeAsync();

        var session = await _repo.CreateSessionAsync(MakeSession());
        await _repo.CompleteSessionAsync(session.Id, DateTimeOffset.UtcNow, 3, false);

        var sessions = await _repo.GetRecentSessionsAsync(1);
        Assert.False(sessions[0].CompletedNormally);
        Assert.Equal(3, sessions[0].MeasurementCount);
    }

    [Fact]
    public async Task GetRecentSessionsAsync_ReturnsOrderedByStartDesc()
    {
        await _repo.InitializeAsync();

        var s1 = MakeSession();
        s1 = new MonitoringSession
        {
            Id = 0,
            StartedAt = DateTimeOffset.UtcNow.AddHours(-2),
            Target = "1.1.1.1",
            IntervalSeconds = 5,
            ProbesPerMeasurement = 4,
            TimeoutMs = 3000
        };
        var s2 = MakeSession("8.8.8.8");

        await _repo.CreateSessionAsync(s1);
        await _repo.CreateSessionAsync(s2);

        var sessions = await _repo.GetRecentSessionsAsync(10);
        Assert.Equal(2, sessions.Count);
        Assert.Equal("8.8.8.8", sessions[0].Target);
        Assert.Equal("1.1.1.1", sessions[1].Target);
    }

    [Fact]
    public async Task GetRecentSessionsAsync_EmptyDatabase_ReturnsEmpty()
    {
        await _repo.InitializeAsync();
        var sessions = await _repo.GetRecentSessionsAsync();
        Assert.Empty(sessions);
    }

    // ===============================
    // Saving and retrieving measurements
    // ===============================

    [Fact]
    public async Task SaveAndRetrieve_Measurement_AllFieldsPreserved()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        var original = new NetworkMeasurement
        {
            Timestamp = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero),
            CycleNumber = 1,
            Target = "1.1.1.1",
            IsConnected = true,
            PacketsSent = 4,
            PacketsReceived = 3,
            PacketLossPercent = 25.0,
            MinLatencyMs = 2.5,
            MaxLatencyMs = 15.0,
            AvgLatencyMs = 8.0,
            JitterMs = 3.5,
            DnsResolutionMs = 12.3,
            GatewayLatencyMs = 0.8,
            HealthStatus = NetworkHealthStatus.Degraded
        };

        await _repo.SaveMeasurementAsync(session.Id, original);

        var retrieved = await _repo.GetMeasurementsBySessionAsync(session.Id);
        Assert.Single(retrieved);

        var m = retrieved[0];
        Assert.Equal(1, m.CycleNumber);
        Assert.Equal("1.1.1.1", m.Target);
        Assert.True(m.IsConnected);
        Assert.Equal(4, m.PacketsSent);
        Assert.Equal(3, m.PacketsReceived);
        Assert.Equal(25.0, m.PacketLossPercent);
        Assert.Equal(2.5, m.MinLatencyMs);
        Assert.Equal(15.0, m.MaxLatencyMs);
        Assert.Equal(8.0, m.AvgLatencyMs);
        Assert.Equal(3.5, m.JitterMs);
        Assert.Equal(12.3, m.DnsResolutionMs);
        Assert.Equal(0.8, m.GatewayLatencyMs);
        Assert.Equal(NetworkHealthStatus.Degraded, m.HealthStatus);
    }

    [Fact]
    public async Task SaveAndRetrieve_NullableFieldsNull_PreservedAsNull()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        await _repo.SaveMeasurementAsync(session.Id, MakeDisconnected());

        var retrieved = await _repo.GetMeasurementsBySessionAsync(session.Id);
        var m = retrieved[0];

        Assert.Null(m.MinLatencyMs);
        Assert.Null(m.MaxLatencyMs);
        Assert.Null(m.AvgLatencyMs);
        Assert.Null(m.JitterMs);
        Assert.Null(m.DnsResolutionMs);
        Assert.Null(m.GatewayLatencyMs);
    }

    // ===============================
    // Session association
    // ===============================

    [Fact]
    public async Task Measurements_AssociatedWithCorrectSession()
    {
        await _repo.InitializeAsync();
        var s1 = await _repo.CreateSessionAsync(MakeSession("1.1.1.1"));
        var s2 = await _repo.CreateSessionAsync(MakeSession("8.8.8.8"));

        await _repo.SaveMeasurementAsync(s1.Id, MakeHealthy(1, "1.1.1.1"));
        await _repo.SaveMeasurementAsync(s1.Id, MakeHealthy(2, "1.1.1.1"));
        await _repo.SaveMeasurementAsync(s2.Id, MakeHealthy(1, "8.8.8.8"));

        var m1 = await _repo.GetMeasurementsBySessionAsync(s1.Id);
        var m2 = await _repo.GetMeasurementsBySessionAsync(s2.Id);

        Assert.Equal(2, m1.Count);
        Assert.Single(m2);
        Assert.All(m1, m => Assert.Equal("1.1.1.1", m.Target));
        Assert.All(m2, m => Assert.Equal("8.8.8.8", m.Target));
    }

    [Fact]
    public async Task GetMeasurementsBySessionAsync_OrderedByCycleNumber()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy(3));
        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy(1));
        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy(2));

        var measurements = await _repo.GetMeasurementsBySessionAsync(session.Id);
        Assert.Equal(3, measurements.Count);
        Assert.Equal(1, measurements[0].CycleNumber);
        Assert.Equal(2, measurements[1].CycleNumber);
        Assert.Equal(3, measurements[2].CycleNumber);
    }

    // ===============================
    // Recent measurements
    // ===============================

    [Fact]
    public async Task GetRecentMeasurementsAsync_ReturnsAcrossSessions()
    {
        await _repo.InitializeAsync();
        var s1 = await _repo.CreateSessionAsync(MakeSession());
        var s2 = await _repo.CreateSessionAsync(MakeSession("8.8.8.8"));

        await _repo.SaveMeasurementAsync(s1.Id, MakeHealthy(1));
        await _repo.SaveMeasurementAsync(s2.Id, MakeHealthy(1, "8.8.8.8"));

        var recent = await _repo.GetRecentMeasurementsAsync(50);
        Assert.Equal(2, recent.Count);
    }

    [Fact]
    public async Task GetRecentMeasurementsAsync_RespectsLimit()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        for (var i = 1; i <= 10; i++)
            await _repo.SaveMeasurementAsync(session.Id, MakeHealthy(i));

        var recent = await _repo.GetRecentMeasurementsAsync(3);
        Assert.Equal(3, recent.Count);
    }

    [Fact]
    public async Task GetRecentMeasurementsAsync_EmptyDatabase_ReturnsEmpty()
    {
        await _repo.InitializeAsync();
        var recent = await _repo.GetRecentMeasurementsAsync();
        Assert.Empty(recent);
    }

    // ===============================
    // Time-range queries
    // ===============================

    [Fact]
    public async Task GetMeasurementsByTimeRangeAsync_FiltersCorrectly()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        var baseTime = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 5; i++)
        {
            var m = new NetworkMeasurement
            {
                Timestamp = baseTime.AddMinutes(i * 10),
                CycleNumber = i + 1,
                Target = "1.1.1.1",
                IsConnected = true,
                PacketsSent = 4,
                PacketsReceived = 4,
                PacketLossPercent = 0.0,
                AvgLatencyMs = 5.0,
                HealthStatus = NetworkHealthStatus.Healthy
            };
            await _repo.SaveMeasurementAsync(session.Id, m);
        }

        // Range that includes minutes 10–30 (indices 1, 2, 3)
        var from = baseTime.AddMinutes(10);
        var to = baseTime.AddMinutes(30);
        var result = await _repo.GetMeasurementsByTimeRangeAsync(from, to);

        Assert.Equal(3, result.Count);
        Assert.All(result, m =>
        {
            Assert.True(m.Timestamp >= from);
            Assert.True(m.Timestamp <= to);
        });
    }

    [Fact]
    public async Task GetMeasurementsByTimeRangeAsync_NoMatchingData_ReturnsEmpty()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());
        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy());

        var future = DateTimeOffset.UtcNow.AddDays(100);
        var result = await _repo.GetMeasurementsByTimeRangeAsync(future, future.AddHours(1));
        Assert.Empty(result);
    }

    // ===============================
    // Aggregate statistics
    // ===============================

    [Fact]
    public async Task GetSessionAggregateAsync_ComputesCorrectly()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy(1));
        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy(2));
        await _repo.SaveMeasurementAsync(session.Id, MakeDisconnected(3));

        var agg = await _repo.GetSessionAggregateAsync(session.Id);

        Assert.NotNull(agg);
        Assert.Equal(3, agg.TotalMeasurements);
        Assert.Equal(2, agg.ConnectedCount);
        Assert.Equal(1, agg.DisconnectedCount);
        Assert.True(agg.UptimePercent > 60 && agg.UptimePercent < 70);
        Assert.NotNull(agg.AvgLatencyMs);
        Assert.NotNull(agg.MinLatencyMs);
        Assert.NotNull(agg.MaxLatencyMs);
        Assert.NotNull(agg.AvgJitterMs);
        Assert.NotNull(agg.PeriodStart);
        Assert.NotNull(agg.PeriodEnd);
        Assert.Equal(2, agg.HealthBreakdown[NetworkHealthStatus.Healthy]);
        Assert.Equal(1, agg.HealthBreakdown[NetworkHealthStatus.Disconnected]);
        Assert.Equal(0, agg.HealthBreakdown[NetworkHealthStatus.Degraded]);
        Assert.Equal(0, agg.HealthBreakdown[NetworkHealthStatus.Unstable]);
    }

    [Fact]
    public async Task GetSessionAggregateAsync_EmptySession_ReturnsNull()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        var agg = await _repo.GetSessionAggregateAsync(session.Id);
        Assert.Null(agg);
    }

    [Fact]
    public async Task GetSessionAggregateAsync_AllDisconnected_UptimeZero()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        await _repo.SaveMeasurementAsync(session.Id, MakeDisconnected(1));
        await _repo.SaveMeasurementAsync(session.Id, MakeDisconnected(2));

        var agg = await _repo.GetSessionAggregateAsync(session.Id);

        Assert.NotNull(agg);
        Assert.Equal(0.0, agg.UptimePercent);
        Assert.Equal(2, agg.DisconnectedCount);
        Assert.Null(agg.AvgLatencyMs);
    }

    [Fact]
    public async Task GetTimeRangeAggregateAsync_ComputesFromRange()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        var baseTime = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 3; i++)
        {
            var m = new NetworkMeasurement
            {
                Timestamp = baseTime.AddMinutes(i * 5),
                CycleNumber = i + 1,
                Target = "1.1.1.1",
                IsConnected = true,
                PacketsSent = 4,
                PacketsReceived = 4,
                PacketLossPercent = 0.0,
                AvgLatencyMs = 5.0 + i,
                HealthStatus = NetworkHealthStatus.Healthy
            };
            await _repo.SaveMeasurementAsync(session.Id, m);
        }

        var agg = await _repo.GetTimeRangeAggregateAsync(
            baseTime, baseTime.AddMinutes(15));

        Assert.NotNull(agg);
        Assert.Equal(3, agg.TotalMeasurements);
        Assert.Equal(100.0, agg.UptimePercent);
    }

    [Fact]
    public async Task GetTimeRangeAggregateAsync_NoData_ReturnsNull()
    {
        await _repo.InitializeAsync();
        var future = DateTimeOffset.UtcNow.AddDays(100);
        var agg = await _repo.GetTimeRangeAggregateAsync(future, future.AddHours(1));
        Assert.Null(agg);
    }

    // ===============================
    // Retention / deletion
    // ===============================

    [Fact]
    public async Task DeleteMeasurementsOlderThanAsync_DeletesOldKeepsNew()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());

        var oldTime = DateTimeOffset.UtcNow.AddDays(-30);
        var newTime = DateTimeOffset.UtcNow;

        var oldMeasurement = new NetworkMeasurement
        {
            Timestamp = oldTime,
            CycleNumber = 1,
            Target = "1.1.1.1",
            IsConnected = true,
            PacketsSent = 4,
            PacketsReceived = 4,
            PacketLossPercent = 0.0,
            HealthStatus = NetworkHealthStatus.Healthy
        };

        await _repo.SaveMeasurementAsync(session.Id, oldMeasurement);
        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy(2));

        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var deleted = await _repo.DeleteMeasurementsOlderThanAsync(cutoff);

        Assert.Equal(1, deleted);

        var remaining = await _repo.GetMeasurementsBySessionAsync(session.Id);
        Assert.Single(remaining);
    }

    [Fact]
    public async Task DeleteMeasurementsOlderThanAsync_NothingToDelete_ReturnsZero()
    {
        await _repo.InitializeAsync();
        var deleted = await _repo.DeleteMeasurementsOlderThanAsync(DateTimeOffset.UtcNow);
        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteEmptySessionsAsync_RemovesSessionsWithNoMeasurements()
    {
        await _repo.InitializeAsync();

        var s1 = await _repo.CreateSessionAsync(MakeSession());
        var s2 = await _repo.CreateSessionAsync(MakeSession("8.8.8.8"));

        // Only s1 gets measurements
        await _repo.SaveMeasurementAsync(s1.Id, MakeHealthy());

        var deleted = await _repo.DeleteEmptySessionsAsync();
        Assert.Equal(1, deleted);

        var sessions = await _repo.GetRecentSessionsAsync();
        Assert.Single(sessions);
        Assert.Equal(s1.Id, sessions[0].Id);
    }

    [Fact]
    public async Task DeleteEmptySessionsAsync_NoEmptySessions_ReturnsZero()
    {
        await _repo.InitializeAsync();
        var session = await _repo.CreateSessionAsync(MakeSession());
        await _repo.SaveMeasurementAsync(session.Id, MakeHealthy());

        var deleted = await _repo.DeleteEmptySessionsAsync();
        Assert.Equal(0, deleted);
    }

    // ===============================
    // Multiple sessions
    // ===============================

    [Fact]
    public async Task MultipleSessions_IndependentData()
    {
        await _repo.InitializeAsync();

        var s1 = await _repo.CreateSessionAsync(MakeSession("1.1.1.1"));
        var s2 = await _repo.CreateSessionAsync(MakeSession("8.8.8.8"));
        var s3 = await _repo.CreateSessionAsync(MakeSession("google.com"));

        await _repo.SaveMeasurementAsync(s1.Id, MakeHealthy(1, "1.1.1.1"));
        await _repo.SaveMeasurementAsync(s1.Id, MakeHealthy(2, "1.1.1.1"));
        await _repo.SaveMeasurementAsync(s2.Id, MakeDisconnected(1));
        await _repo.SaveMeasurementAsync(s3.Id, MakeHealthy(1, "google.com"));
        await _repo.SaveMeasurementAsync(s3.Id, MakeHealthy(2, "google.com"));
        await _repo.SaveMeasurementAsync(s3.Id, MakeHealthy(3, "google.com"));

        var m1 = await _repo.GetMeasurementsBySessionAsync(s1.Id);
        var m2 = await _repo.GetMeasurementsBySessionAsync(s2.Id);
        var m3 = await _repo.GetMeasurementsBySessionAsync(s3.Id);

        Assert.Equal(2, m1.Count);
        Assert.Single(m2);
        Assert.Equal(3, m3.Count);

        var agg1 = await _repo.GetSessionAggregateAsync(s1.Id);
        var agg2 = await _repo.GetSessionAggregateAsync(s2.Id);

        Assert.Equal(100.0, agg1!.UptimePercent);
        Assert.Equal(0.0, agg2!.UptimePercent);
    }

    // ===============================
    // Session config preservation
    // ===============================

    [Fact]
    public async Task Session_ConfigFieldsPreservedOnRetrieve()
    {
        await _repo.InitializeAsync();

        var session = new MonitoringSession
        {
            Id = 0,
            StartedAt = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero),
            Target = "google.com",
            IntervalSeconds = 10,
            ProbesPerMeasurement = 8,
            TimeoutMs = 5000
        };

        var created = await _repo.CreateSessionAsync(session);
        var sessions = await _repo.GetRecentSessionsAsync();

        Assert.Single(sessions);
        var s = sessions[0];
        Assert.Equal(created.Id, s.Id);
        Assert.Equal("google.com", s.Target);
        Assert.Equal(10, s.IntervalSeconds);
        Assert.Equal(8, s.ProbesPerMeasurement);
        Assert.Equal(5000, s.TimeoutMs);
    }
}
