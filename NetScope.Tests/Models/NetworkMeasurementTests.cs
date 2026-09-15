using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class NetworkMeasurementTests
{
    [Fact]
    public void Create_HealthyMeasurement_AllFieldsSet()
    {
        var ts = DateTimeOffset.UtcNow;
        var m = new NetworkMeasurement
        {
            Timestamp = ts,
            CycleNumber = 1,
            Target = "1.1.1.1",
            IsConnected = true,
            PacketsSent = 4,
            PacketsReceived = 4,
            PacketLossPercent = 0.0,
            MinLatencyMs = 3.0,
            MaxLatencyMs = 8.0,
            AvgLatencyMs = 5.5,
            JitterMs = 1.2,
            DnsResolutionMs = 12.3,
            GatewayLatencyMs = 0.8,
            HealthStatus = NetworkHealthStatus.Healthy
        };

        Assert.Equal(ts, m.Timestamp);
        Assert.Equal(1, m.CycleNumber);
        Assert.Equal("1.1.1.1", m.Target);
        Assert.True(m.IsConnected);
        Assert.Equal(4, m.PacketsSent);
        Assert.Equal(4, m.PacketsReceived);
        Assert.Equal(0.0, m.PacketLossPercent);
        Assert.Equal(3.0, m.MinLatencyMs);
        Assert.Equal(8.0, m.MaxLatencyMs);
        Assert.Equal(5.5, m.AvgLatencyMs);
        Assert.Equal(1.2, m.JitterMs);
        Assert.Equal(12.3, m.DnsResolutionMs);
        Assert.Equal(0.8, m.GatewayLatencyMs);
        Assert.Equal(NetworkHealthStatus.Healthy, m.HealthStatus);
    }

    [Fact]
    public void Create_DisconnectedMeasurement_NullableFieldsNull()
    {
        var m = new NetworkMeasurement
        {
            Timestamp = DateTimeOffset.UtcNow,
            CycleNumber = 5,
            Target = "10.0.0.1",
            IsConnected = false,
            PacketsSent = 4,
            PacketsReceived = 0,
            PacketLossPercent = 100.0,
            HealthStatus = NetworkHealthStatus.Disconnected
        };

        Assert.False(m.IsConnected);
        Assert.Equal(100.0, m.PacketLossPercent);
        Assert.Null(m.MinLatencyMs);
        Assert.Null(m.MaxLatencyMs);
        Assert.Null(m.AvgLatencyMs);
        Assert.Null(m.JitterMs);
        Assert.Null(m.DnsResolutionMs);
        Assert.Null(m.GatewayLatencyMs);
        Assert.Equal(NetworkHealthStatus.Disconnected, m.HealthStatus);
    }

    [Fact]
    public void Create_MeasurementWithoutOptionalDnsGateway_NullOptionals()
    {
        var m = new NetworkMeasurement
        {
            Timestamp = DateTimeOffset.UtcNow,
            CycleNumber = 1,
            Target = "1.1.1.1",
            IsConnected = true,
            PacketsSent = 4,
            PacketsReceived = 4,
            PacketLossPercent = 0.0,
            AvgLatencyMs = 5.0,
            HealthStatus = NetworkHealthStatus.Healthy
        };

        Assert.Null(m.DnsResolutionMs);
        Assert.Null(m.GatewayLatencyMs);
    }
}
