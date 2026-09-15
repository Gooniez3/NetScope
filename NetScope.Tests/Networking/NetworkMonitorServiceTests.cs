using NetScope.Core.Models;
using NetScope.Core.Networking;
using NetScope.Infrastructure.Network;

namespace NetScope.Tests.Networking;

public class NetworkMonitorServiceTests
{
    /// <summary>
    /// Fake ping service that returns deterministic results without any real network I/O.
    /// </summary>
    private sealed class FakePingService : IPingService
    {
        private readonly Queue<PingStatistics> _results = new();
        private readonly TimeSpan _delay;
        public int CallCount { get; private set; }
        public List<DateTimeOffset> CallTimestamps { get; } = [];

        public FakePingService(TimeSpan delay = default)
        {
            _delay = delay;
        }

        public void Enqueue(PingStatistics stats) => _results.Enqueue(stats);

        public void EnqueueHealthy(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                Enqueue(new PingStatistics
                {
                    Sent = 4,
                    Received = 4,
                    PacketLossPercent = 0.0,
                    MinRoundTripMs = 3.0,
                    MaxRoundTripMs = 8.0,
                    AvgRoundTripMs = 5.0,
                    JitterMs = 1.0
                });
            }
        }

        public void EnqueueDisconnected(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                Enqueue(new PingStatistics
                {
                    Sent = 4,
                    Received = 0,
                    PacketLossPercent = 100.0
                });
            }
        }

        public async Task<(IReadOnlyList<PingResult> Results, PingStatistics Statistics)> PingAsync(
            PingOptions options,
            IProgress<PingResult>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CallTimestamps.Add(DateTimeOffset.UtcNow);

            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, cancellationToken);

            var stats = _results.Count > 0
                ? _results.Dequeue()
                : new PingStatistics
                {
                    Sent = options.Count,
                    Received = options.Count,
                    PacketLossPercent = 0.0,
                    MinRoundTripMs = 5.0,
                    MaxRoundTripMs = 5.0,
                    AvgRoundTripMs = 5.0,
                    JitterMs = 0.0
                };

            return (Array.Empty<PingResult>(), stats);
        }
    }

    // --- MaxCycles produces exact count ---

    [Fact]
    public async Task MonitorAsync_MaxCycles3_ProducesExactly3Measurements()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueHealthy(3);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions
        {
            Target = "1.1.1.1",
            IntervalSeconds = 1,
            MaxCycles = 3,
            ProbesPerMeasurement = 4
        };

        var measurements = new List<NetworkMeasurement>();
        await foreach (var m in monitor.MonitorAsync(options))
        {
            measurements.Add(m);
        }

        Assert.Equal(3, measurements.Count);
        Assert.Equal(1, measurements[0].CycleNumber);
        Assert.Equal(2, measurements[1].CycleNumber);
        Assert.Equal(3, measurements[2].CycleNumber);
    }

    [Fact]
    public async Task MonitorAsync_MaxCycles1_ProducesExactly1Measurement()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueHealthy(1);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions
        {
            Target = "1.1.1.1",
            IntervalSeconds = 1,
            MaxCycles = 1
        };

        var measurements = new List<NetworkMeasurement>();
        await foreach (var m in monitor.MonitorAsync(options))
        {
            measurements.Add(m);
        }

        Assert.Single(measurements);
    }

    // --- Cancellation ---

    [Fact]
    public async Task MonitorAsync_CancelledDuringInterval_StopsCleanly()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueHealthy(10);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions
        {
            Target = "1.1.1.1",
            IntervalSeconds = 60,
            MaxCycles = null
        };

        using var cts = new CancellationTokenSource();
        var measurements = new List<NetworkMeasurement>();

        await foreach (var m in monitor.MonitorAsync(options, cts.Token))
        {
            measurements.Add(m);
            cts.Cancel(); // Cancel after first measurement
        }

        Assert.Single(measurements);
    }

    [Fact]
    public async Task MonitorAsync_CancelledBeforeStart_ProducesNothing()
    {
        var fakePing = new FakePingService();
        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "1.1.1.1", IntervalSeconds = 1 };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var measurements = new List<NetworkMeasurement>();
        await foreach (var m in monitor.MonitorAsync(options, cts.Token))
        {
            measurements.Add(m);
        }

        Assert.Empty(measurements);
    }

    // --- Health classification integration ---

    [Fact]
    public async Task MonitorAsync_AllProbesSucceed_HealthStatusHealthy()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueHealthy(1);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "1.1.1.1", MaxCycles = 1 };

        NetworkMeasurement? result = null;
        await foreach (var m in monitor.MonitorAsync(options))
        {
            result = m;
        }

        Assert.NotNull(result);
        Assert.Equal(NetworkHealthStatus.Healthy, result.HealthStatus);
        Assert.True(result.IsConnected);
    }

    [Fact]
    public async Task MonitorAsync_AllProbesFail_HealthStatusDisconnected()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueDisconnected(1);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "1.1.1.1", MaxCycles = 1 };

        NetworkMeasurement? result = null;
        await foreach (var m in monitor.MonitorAsync(options))
        {
            result = m;
        }

        Assert.NotNull(result);
        Assert.Equal(NetworkHealthStatus.Disconnected, result.HealthStatus);
        Assert.False(result.IsConnected);
        Assert.Equal(100.0, result.PacketLossPercent);
    }

    [Fact]
    public async Task MonitorAsync_DegradedMetrics_HealthStatusDegraded()
    {
        var fakePing = new FakePingService();
        fakePing.Enqueue(new PingStatistics
        {
            Sent = 4,
            Received = 4,
            PacketLossPercent = 0.0,
            MinRoundTripMs = 100.0,
            MaxRoundTripMs = 200.0,
            AvgRoundTripMs = 150.0,
            JitterMs = 5.0
        });

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "1.1.1.1", MaxCycles = 1 };

        NetworkMeasurement? result = null;
        await foreach (var m in monitor.MonitorAsync(options))
        {
            result = m;
        }

        Assert.NotNull(result);
        Assert.Equal(NetworkHealthStatus.Degraded, result.HealthStatus);
    }

    [Fact]
    public async Task MonitorAsync_UnstableMetrics_HealthStatusUnstable()
    {
        var fakePing = new FakePingService();
        fakePing.Enqueue(new PingStatistics
        {
            Sent = 4,
            Received = 2,
            PacketLossPercent = 50.0,
            MinRoundTripMs = 100.0,
            MaxRoundTripMs = 800.0,
            AvgRoundTripMs = 450.0,
            JitterMs = 200.0
        });

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "1.1.1.1", MaxCycles = 1 };

        NetworkMeasurement? result = null;
        await foreach (var m in monitor.MonitorAsync(options))
        {
            result = m;
        }

        Assert.NotNull(result);
        Assert.Equal(NetworkHealthStatus.Unstable, result.HealthStatus);
    }

    // --- Measurement fields ---

    [Fact]
    public async Task MonitorAsync_TargetCarriedThrough()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueHealthy(1);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "8.8.4.4", MaxCycles = 1 };

        await foreach (var m in monitor.MonitorAsync(options))
        {
            Assert.Equal("8.8.4.4", m.Target);
        }
    }

    [Fact]
    public async Task MonitorAsync_PacketStatsReflectPingResults()
    {
        var fakePing = new FakePingService();
        fakePing.Enqueue(new PingStatistics
        {
            Sent = 8,
            Received = 6,
            PacketLossPercent = 25.0,
            MinRoundTripMs = 10.0,
            MaxRoundTripMs = 80.0,
            AvgRoundTripMs = 40.0,
            JitterMs = 15.0
        });

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "1.1.1.1", MaxCycles = 1, ProbesPerMeasurement = 8 };

        await foreach (var m in monitor.MonitorAsync(options))
        {
            Assert.Equal(8, m.PacketsSent);
            Assert.Equal(6, m.PacketsReceived);
            Assert.Equal(25.0, m.PacketLossPercent);
            Assert.Equal(10.0, m.MinLatencyMs);
            Assert.Equal(80.0, m.MaxLatencyMs);
            Assert.Equal(40.0, m.AvgLatencyMs);
            Assert.Equal(15.0, m.JitterMs);
        }
    }

    // --- No overlapping cycles ---

    [Fact]
    public async Task MonitorAsync_SlowMeasurement_CyclesDoNotOverlap()
    {
        // Simulate a measurement that takes 200ms, with a 1-second interval.
        // Verify cycles are sequential (call count matches measurement count).
        var fakePing = new FakePingService(delay: TimeSpan.FromMilliseconds(200));
        fakePing.EnqueueHealthy(3);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions
        {
            Target = "1.1.1.1",
            IntervalSeconds = 1,
            MaxCycles = 3
        };

        var measurements = new List<NetworkMeasurement>();
        await foreach (var m in monitor.MonitorAsync(options))
        {
            measurements.Add(m);
        }

        Assert.Equal(3, measurements.Count);
        Assert.Equal(3, fakePing.CallCount);

        // Verify timestamps are strictly increasing
        for (var i = 1; i < fakePing.CallTimestamps.Count; i++)
        {
            Assert.True(fakePing.CallTimestamps[i] > fakePing.CallTimestamps[i - 1],
                $"Cycle {i + 1} should start after cycle {i}");
        }
    }

    // --- Failure recovery ---

    [Fact]
    public async Task MonitorAsync_MixedSuccessAndFailure_ContinuesAfterFailure()
    {
        var fakePing = new FakePingService();

        // Cycle 1: Healthy
        fakePing.EnqueueHealthy(1);
        // Cycle 2: Disconnected
        fakePing.EnqueueDisconnected(1);
        // Cycle 3: Healthy again (recovered)
        fakePing.EnqueueHealthy(1);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions
        {
            Target = "1.1.1.1",
            IntervalSeconds = 1,
            MaxCycles = 3
        };

        var measurements = new List<NetworkMeasurement>();
        await foreach (var m in monitor.MonitorAsync(options))
        {
            measurements.Add(m);
        }

        Assert.Equal(3, measurements.Count);
        Assert.Equal(NetworkHealthStatus.Healthy, measurements[0].HealthStatus);
        Assert.Equal(NetworkHealthStatus.Disconnected, measurements[1].HealthStatus);
        Assert.Equal(NetworkHealthStatus.Healthy, measurements[2].HealthStatus);
    }

    // --- Validation ---

    [Fact]
    public async Task MonitorAsync_InvalidOptions_Throws()
    {
        var fakePing = new FakePingService();
        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions { Target = "", IntervalSeconds = 5 };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in monitor.MonitorAsync(options))
            {
            }
        });
    }

    // --- Optional DNS not included when disabled ---

    [Fact]
    public async Task MonitorAsync_DnsDisabled_NoDnsMeasurement()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueHealthy(1);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions
        {
            Target = "1.1.1.1",
            MaxCycles = 1,
            MeasureDns = false
        };

        await foreach (var m in monitor.MonitorAsync(options))
        {
            Assert.Null(m.DnsResolutionMs);
        }
    }

    [Fact]
    public async Task MonitorAsync_GatewayDisabled_NoGatewayMeasurement()
    {
        var fakePing = new FakePingService();
        fakePing.EnqueueHealthy(1);

        var monitor = new NetworkMonitorService(fakePing);
        var options = new MonitorOptions
        {
            Target = "1.1.1.1",
            MaxCycles = 1,
            MeasureGateway = false
        };

        await foreach (var m in monitor.MonitorAsync(options))
        {
            Assert.Null(m.GatewayLatencyMs);
        }
    }
}
