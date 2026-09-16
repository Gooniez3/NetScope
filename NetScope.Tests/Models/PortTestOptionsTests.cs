using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class PortTestOptionsTests
{
    [Fact]
    public void Validate_Defaults_Accepted()
    {
        var options = new PortTestOptions { Host = "1.1.1.1" };
        options.Validate();
        Assert.Equal(443, options.Port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyHost_Throws(string? host)
    {
        var options = new PortTestOptions { Host = host! };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Host", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Validate_PortOutOfRange_Throws(int port)
    {
        var options = new PortTestOptions { Host = "127.0.0.1", Port = port };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Port", ex.ParamName);
    }

    [Fact]
    public void Validate_TimeoutBelowMinimum_Throws()
    {
        var options = new PortTestOptions { Host = "127.0.0.1", TimeoutMs = 99 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    [Theory]
    [InlineData("https", 443)]
    [InlineData("SSH", 22)]
    [InlineData("rdp", 3389)]
    public void TryResolvePreset_KnownNames_Resolved(string name, int expected)
    {
        Assert.True(PortTestOptions.TryResolvePreset(name, out var port));
        Assert.Equal(expected, port);
    }

    [Fact]
    public void TryResolvePreset_Unknown_False()
    {
        Assert.False(PortTestOptions.TryResolvePreset("ftp", out _));
    }
}
