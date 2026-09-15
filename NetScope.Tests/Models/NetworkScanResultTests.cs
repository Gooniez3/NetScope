using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class NetworkScanResultTests
{
    private static DiscoveredDevice Device(string ip, double rtt) => new()
    {
        IpAddress = ip,
        ResponseTimeMs = rtt,
        DiscoveryMethod = "ICMP",
        Status = "Online"
    };

    [Fact]
    public void DevicesDiscovered_MatchesDevicesCount()
    {
        var result = new NetworkScanResult
        {
            ScannedRange = "192.168.1.0/24",
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-3),
            CompletedAt = DateTimeOffset.UtcNow,
            AddressesScanned = 254,
            Devices = [Device("192.168.1.1", 1), Device("192.168.1.9", 0.5)]
        };

        Assert.Equal(2, result.DevicesDiscovered);
    }

    [Fact]
    public void Duration_CalculatedFromTimestamps()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddSeconds(3.5);

        var result = new NetworkScanResult
        {
            ScannedRange = "10.0.0.0/24",
            StartedAt = start,
            CompletedAt = end,
            AddressesScanned = 254,
            Devices = []
        };

        Assert.Equal(TimeSpan.FromSeconds(3.5), result.Duration);
    }

    [Fact]
    public void EmptyDevices_ZeroDiscovered()
    {
        var result = new NetworkScanResult
        {
            ScannedRange = "192.168.1.0/24",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            AddressesScanned = 254,
            Devices = []
        };

        Assert.Equal(0, result.DevicesDiscovered);
        Assert.Empty(result.Devices);
    }

    [Fact]
    public void CancelledResult_HasPartialDevices()
    {
        var result = new NetworkScanResult
        {
            ScannedRange = "192.168.1.0/24",
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            AddressesScanned = 254,
            Devices = [Device("192.168.1.1", 1)],
            WasCancelled = true
        };

        Assert.True(result.WasCancelled);
        Assert.Equal(1, result.DevicesDiscovered);
    }

    [Fact]
    public void DiscoveredDevice_AllPropertiesPopulated()
    {
        var device = new DiscoveredDevice
        {
            IpAddress = "192.168.1.1",
            Hostname = "router.local",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            ResponseTimeMs = 2.5,
            DiscoveryMethod = "ICMP",
            Status = "Online"
        };

        Assert.Equal("192.168.1.1", device.IpAddress);
        Assert.Equal("router.local", device.Hostname);
        Assert.Equal("AA:BB:CC:DD:EE:FF", device.MacAddress);
        Assert.Equal(2.5, device.ResponseTimeMs);
        Assert.Equal("ICMP", device.DiscoveryMethod);
        Assert.Equal("Online", device.Status);
    }

    [Fact]
    public void DiscoveredDevice_OptionalFieldsNullable()
    {
        var device = new DiscoveredDevice
        {
            IpAddress = "10.0.0.1",
            ResponseTimeMs = 1.0,
            DiscoveryMethod = "ICMP",
            Status = "Online"
        };

        Assert.Null(device.Hostname);
        Assert.Null(device.MacAddress);
    }
}
