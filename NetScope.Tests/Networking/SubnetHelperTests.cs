using System.Net;
using NetScope.Core.Networking;

namespace NetScope.Tests.Networking;

public class SubnetHelperTests
{
    // --- ParseCidr ---

    [Fact]
    public void ParseCidr_ValidSlash24_ReturnsCorrectNetworkAndPrefix()
    {
        var (network, prefix) = SubnetHelper.ParseCidr("192.168.1.0/24");
        Assert.Equal(IPAddress.Parse("192.168.1.0"), network);
        Assert.Equal(24, prefix);
    }

    [Fact]
    public void ParseCidr_ValidSlash16_ReturnsCorrectPrefix()
    {
        var (network, prefix) = SubnetHelper.ParseCidr("10.0.0.0/16");
        Assert.Equal(IPAddress.Parse("10.0.0.0"), network);
        Assert.Equal(16, prefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.0")]
    [InlineData("not-cidr")]
    [InlineData("192.168.1.0/24/extra")]
    public void ParseCidr_InvalidFormat_ThrowsFormatException(string cidr)
    {
        Assert.Throws<FormatException>(() => SubnetHelper.ParseCidr(cidr));
    }

    [Fact]
    public void ParseCidr_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SubnetHelper.ParseCidr(null!));
    }

    [Fact]
    public void ParseCidr_InvalidPrefix_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => SubnetHelper.ParseCidr("192.168.1.0/33"));
    }

    [Fact]
    public void ParseCidr_NegativePrefix_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => SubnetHelper.ParseCidr("192.168.1.0/-1"));
    }

    // --- PrefixToMask ---

    [Theory]
    [InlineData(24, "255.255.255.0")]
    [InlineData(16, "255.255.0.0")]
    [InlineData(8, "255.0.0.0")]
    [InlineData(32, "255.255.255.255")]
    [InlineData(0, "0.0.0.0")]
    [InlineData(25, "255.255.255.128")]
    public void PrefixToMask_KnownValues(int prefix, string expectedMask)
    {
        Assert.Equal(IPAddress.Parse(expectedMask), SubnetHelper.PrefixToMask(prefix));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(33)]
    public void PrefixToMask_OutOfRange_Throws(int prefix)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SubnetHelper.PrefixToMask(prefix));
    }

    // --- GetNetworkAddress ---

    [Fact]
    public void GetNetworkAddress_Slash24_MasksCorrectly()
    {
        var network = SubnetHelper.GetNetworkAddress(IPAddress.Parse("192.168.1.100"), 24);
        Assert.Equal(IPAddress.Parse("192.168.1.0"), network);
    }

    [Fact]
    public void GetNetworkAddress_Slash16_MasksCorrectly()
    {
        var network = SubnetHelper.GetNetworkAddress(IPAddress.Parse("10.20.30.40"), 16);
        Assert.Equal(IPAddress.Parse("10.20.0.0"), network);
    }

    [Fact]
    public void GetNetworkAddress_Slash25_MasksCorrectly()
    {
        var network = SubnetHelper.GetNetworkAddress(IPAddress.Parse("192.168.1.200"), 25);
        Assert.Equal(IPAddress.Parse("192.168.1.128"), network);
    }

    // --- GetBroadcastAddress ---

    [Fact]
    public void GetBroadcastAddress_Slash24()
    {
        var broadcast = SubnetHelper.GetBroadcastAddress(IPAddress.Parse("192.168.1.0"), 24);
        Assert.Equal(IPAddress.Parse("192.168.1.255"), broadcast);
    }

    [Fact]
    public void GetBroadcastAddress_Slash16()
    {
        var broadcast = SubnetHelper.GetBroadcastAddress(IPAddress.Parse("10.20.0.0"), 16);
        Assert.Equal(IPAddress.Parse("10.20.255.255"), broadcast);
    }

    // --- GetHostAddresses ---

    [Fact]
    public void GetHostAddresses_Slash24_Returns254Hosts()
    {
        var hosts = SubnetHelper.GetHostAddresses(IPAddress.Parse("192.168.1.0"), 24);
        Assert.Equal(254, hosts.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), hosts[0]);
        Assert.Equal(IPAddress.Parse("192.168.1.254"), hosts[^1]);
    }

    [Fact]
    public void GetHostAddresses_Slash24_ExcludesNetworkAndBroadcast()
    {
        var hosts = SubnetHelper.GetHostAddresses(IPAddress.Parse("192.168.1.0"), 24);
        Assert.DoesNotContain(IPAddress.Parse("192.168.1.0"), hosts);
        Assert.DoesNotContain(IPAddress.Parse("192.168.1.255"), hosts);
    }

    [Fact]
    public void GetHostAddresses_Slash30_Returns2Hosts()
    {
        var hosts = SubnetHelper.GetHostAddresses(IPAddress.Parse("10.0.0.0"), 30);
        Assert.Equal(2, hosts.Count);
        Assert.Equal(IPAddress.Parse("10.0.0.1"), hosts[0]);
        Assert.Equal(IPAddress.Parse("10.0.0.2"), hosts[1]);
    }

    [Fact]
    public void GetHostAddresses_Slash31_ReturnsEmpty()
    {
        var hosts = SubnetHelper.GetHostAddresses(IPAddress.Parse("10.0.0.0"), 31);
        Assert.Empty(hosts);
    }

    [Fact]
    public void GetHostAddresses_Slash32_ReturnsEmpty()
    {
        var hosts = SubnetHelper.GetHostAddresses(IPAddress.Parse("10.0.0.1"), 32);
        Assert.Empty(hosts);
    }

    [Fact]
    public void GetHostAddresses_Slash25_Returns126Hosts()
    {
        var hosts = SubnetHelper.GetHostAddresses(IPAddress.Parse("192.168.1.128"), 25);
        Assert.Equal(126, hosts.Count);
        Assert.Equal(IPAddress.Parse("192.168.1.129"), hosts[0]);
        Assert.Equal(IPAddress.Parse("192.168.1.254"), hosts[^1]);
    }

    // --- ToCidrString ---

    [Fact]
    public void ToCidrString_FormatsCorrectly()
    {
        var cidr = SubnetHelper.ToCidrString(IPAddress.Parse("192.168.1.0"), 24);
        Assert.Equal("192.168.1.0/24", cidr);
    }

    // --- InferSubnetFromInterface ---

    [Fact]
    public void InferSubnet_WithPrefixInfo_ReturnsCorrectSubnet()
    {
        var result = SubnetHelper.InferSubnetFromInterface(
            "192.168.1.100",
            [("192.168.1.100", 24)]);

        Assert.NotNull(result);
        Assert.Equal(IPAddress.Parse("192.168.1.0"), result.Value.Network);
        Assert.Equal(24, result.Value.PrefixLength);
    }

    [Fact]
    public void InferSubnet_NoPrefixInfo_FallsBackTo24()
    {
        var result = SubnetHelper.InferSubnetFromInterface("10.0.0.50", []);

        Assert.NotNull(result);
        Assert.Equal(IPAddress.Parse("10.0.0.0"), result.Value.Network);
        Assert.Equal(24, result.Value.PrefixLength);
    }

    [Fact]
    public void InferSubnet_InvalidIp_ReturnsNull()
    {
        var result = SubnetHelper.InferSubnetFromInterface("not-an-ip", []);
        Assert.Null(result);
    }
}
