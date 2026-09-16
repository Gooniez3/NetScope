using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Tests.Models;

public class PingOptionsTests
{
    // --- Valid configurations ---

    [Fact]
    public void Validate_DefaultValues_Accepted()
    {
        var options = new PingOptions { Target = "1.1.1.1" };
        options.Validate(); // should not throw
    }

    [Fact]
    public void Validate_AllExplicitValues_Accepted()
    {
        var options = new PingOptions
        {
            Target = "example.com",
            Count = 10,
            IntervalMs = 500,
            TimeoutMs = 5000,
            PayloadSize = 64,
            Ttl = 128
        };
        options.Validate();
    }

    [Fact]
    public void Validate_NullTtl_Accepted()
    {
        var options = new PingOptions { Target = "1.1.1.1", Ttl = null };
        options.Validate();
    }

    // --- Target validation ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrNullTarget_Throws(string? target)
    {
        var options = new PingOptions { Target = target! };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Target", ex.ParamName);
    }

    // --- Count validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_CountBelowMinimum_Throws(int count)
    {
        var options = new PingOptions { Target = "1.1.1.1", Count = count };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Count", ex.ParamName);
    }

    [Fact]
    public void Validate_CountAboveMaximum_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", Count = 1001 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Count", ex.ParamName);
    }

    // --- Interval validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Validate_IntervalBelowMinimum_Throws(int interval)
    {
        var options = new PingOptions { Target = "1.1.1.1", IntervalMs = interval };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("IntervalMs", ex.ParamName);
    }

    [Fact]
    public void Validate_IntervalAboveMaximum_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", IntervalMs = 60_001 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("IntervalMs", ex.ParamName);
    }

    // --- Timeout validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Validate_TimeoutBelowMinimum_Throws(int timeout)
    {
        var options = new PingOptions { Target = "1.1.1.1", TimeoutMs = timeout };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    [Fact]
    public void Validate_TimeoutAboveMaximum_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", TimeoutMs = 30_001 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    // --- Payload size validation ---

    [Fact]
    public void Validate_PayloadSizeBelowMinimum_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", PayloadSize = -1 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("PayloadSize", ex.ParamName);
    }

    [Fact]
    public void Validate_PayloadSizeAboveMaximum_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", PayloadSize = 65_501 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("PayloadSize", ex.ParamName);
    }

    [Fact]
    public void Validate_PayloadSizeZero_Accepted()
    {
        var options = new PingOptions { Target = "1.1.1.1", PayloadSize = 0 };
        options.Validate();
    }

    // --- TTL validation ---

    [Fact]
    public void Validate_TtlBelowMinimum_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", Ttl = 0 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Ttl", ex.ParamName);
    }

    [Fact]
    public void Validate_TtlAboveMaximum_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", Ttl = 256 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Ttl", ex.ParamName);
    }

    // --- Boundary values ---

    [Fact]
    public void Validate_AllMinimumBoundaryValues_Accepted()
    {
        var options = new PingOptions
        {
            Target = "x",
            Count = PingOptions.MinCount,
            IntervalMs = PingOptions.MinIntervalMs,
            TimeoutMs = PingOptions.MinTimeoutMs,
            PayloadSize = PingOptions.MinPayloadSize,
            Ttl = PingOptions.MinTtl
        };
        options.Validate();
    }

    [Fact]
    public void Validate_AllMaximumBoundaryValues_Accepted()
    {
        var options = new PingOptions
        {
            Target = "x",
            Count = PingOptions.MaxCount,
            IntervalMs = PingOptions.MaxIntervalMs,
            TimeoutMs = PingOptions.MaxTimeoutMs,
            PayloadSize = PingOptions.MaxPayloadSize,
            Ttl = PingOptions.MaxTtl
        };
        options.Validate();
    }

    [Fact]
    public void Validate_CountExactlyOneAboveMax_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", Count = PingOptions.MaxCount + 1 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_CountExactlyOneBelowMin_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", Count = PingOptions.MinCount - 1 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_TtlExactlyOneBelowMin_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", Ttl = PingOptions.MinTtl - 1 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_TtlExactlyOneAboveMax_Throws()
    {
        var options = new PingOptions { Target = "1.1.1.1", Ttl = PingOptions.MaxTtl + 1 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_TargetLongerThanMax_Throws()
    {
        var options = new PingOptions { Target = new string('a', HostTarget.MaxLength + 1) };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Target", ex.ParamName);
    }

    [Fact]
    public void Validate_TargetWithNullChar_Throws()
    {
        var options = new PingOptions { Target = "host\0name" };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Target", ex.ParamName);
    }
}
