using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class TracerouteOptionsTests
{
    [Fact]
    public void Validate_DefaultValues_Accepted()
    {
        var options = new TracerouteOptions { Target = "example.com" };
        options.Validate();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyTarget_Throws(string? target)
    {
        var options = new TracerouteOptions { Target = target! };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Target", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxHopsBelowMinimum_Throws(int maxHops)
    {
        var options = new TracerouteOptions { Target = "x", MaxHops = maxHops };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("MaxHops", ex.ParamName);
    }

    [Fact]
    public void Validate_MaxHopsAboveMaximum_Throws()
    {
        var options = new TracerouteOptions { Target = "x", MaxHops = TracerouteOptions.MaxMaxHops + 1 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("MaxHops", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Validate_TimeoutBelowMinimum_Throws(int timeout)
    {
        var options = new TracerouteOptions { Target = "x", TimeoutMs = timeout };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    [Fact]
    public void Validate_TimeoutAboveMaximum_Throws()
    {
        var options = new TracerouteOptions { Target = "x", TimeoutMs = TracerouteOptions.MaxTimeoutMs + 1 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    [Fact]
    public void Validate_BoundaryMinValues_Accepted()
    {
        var options = new TracerouteOptions
        {
            Target = "x",
            MaxHops = TracerouteOptions.MinMaxHops,
            TimeoutMs = TracerouteOptions.MinTimeoutMs
        };
        options.Validate();
    }

    [Fact]
    public void Validate_BoundaryMaxValues_Accepted()
    {
        var options = new TracerouteOptions
        {
            Target = "x",
            MaxHops = TracerouteOptions.MaxMaxHops,
            TimeoutMs = TracerouteOptions.MaxTimeoutMs
        };
        options.Validate();
    }
}
