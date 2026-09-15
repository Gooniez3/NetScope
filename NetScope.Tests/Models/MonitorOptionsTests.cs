using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class MonitorOptionsTests
{
    // --- Valid configurations ---

    [Fact]
    public void Validate_DefaultValues_Accepted()
    {
        var options = new MonitorOptions();
        options.Validate();
    }

    [Fact]
    public void Validate_AllExplicitValues_Accepted()
    {
        var options = new MonitorOptions
        {
            Target = "8.8.8.8",
            IntervalSeconds = 10,
            TimeoutMs = 5000,
            ProbesPerMeasurement = 8,
            MaxCycles = 100,
            MeasureDns = true,
            MeasureGateway = true,
            GatewayAddress = "192.168.1.1"
        };
        options.Validate();
    }

    [Fact]
    public void Validate_NullMaxCycles_Accepted()
    {
        var options = new MonitorOptions { MaxCycles = null };
        options.Validate();
    }

    // --- Target validation ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrNullTarget_Throws(string? target)
    {
        var options = new MonitorOptions { Target = target! };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Target", ex.ParamName);
    }

    // --- IntervalSeconds validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_IntervalBelowMinimum_Throws(int interval)
    {
        var options = new MonitorOptions { IntervalSeconds = interval };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("IntervalSeconds", ex.ParamName);
    }

    [Fact]
    public void Validate_IntervalAboveMaximum_Throws()
    {
        var options = new MonitorOptions { IntervalSeconds = 301 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("IntervalSeconds", ex.ParamName);
    }

    // --- TimeoutMs validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Validate_TimeoutBelowMinimum_Throws(int timeout)
    {
        var options = new MonitorOptions { TimeoutMs = timeout };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    [Fact]
    public void Validate_TimeoutAboveMaximum_Throws()
    {
        var options = new MonitorOptions { TimeoutMs = 30_001 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    // --- ProbesPerMeasurement validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ProbesBelowMinimum_Throws(int probes)
    {
        var options = new MonitorOptions { ProbesPerMeasurement = probes };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("ProbesPerMeasurement", ex.ParamName);
    }

    [Fact]
    public void Validate_ProbesAboveMaximum_Throws()
    {
        var options = new MonitorOptions { ProbesPerMeasurement = 21 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("ProbesPerMeasurement", ex.ParamName);
    }

    // --- MaxCycles validation ---

    [Fact]
    public void Validate_MaxCyclesBelowMinimum_Throws()
    {
        var options = new MonitorOptions { MaxCycles = 0 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("MaxCycles", ex.ParamName);
    }

    [Fact]
    public void Validate_MaxCyclesAboveMaximum_Throws()
    {
        var options = new MonitorOptions { MaxCycles = 100_001 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("MaxCycles", ex.ParamName);
    }

    // --- Boundary values ---

    [Fact]
    public void Validate_AllMinimumBoundaryValues_Accepted()
    {
        var options = new MonitorOptions
        {
            Target = "x",
            IntervalSeconds = MonitorOptions.MinIntervalSeconds,
            TimeoutMs = MonitorOptions.MinTimeoutMs,
            ProbesPerMeasurement = MonitorOptions.MinProbes,
            MaxCycles = MonitorOptions.MinMaxCycles
        };
        options.Validate();
    }

    [Fact]
    public void Validate_AllMaximumBoundaryValues_Accepted()
    {
        var options = new MonitorOptions
        {
            Target = "x",
            IntervalSeconds = MonitorOptions.MaxIntervalSeconds,
            TimeoutMs = MonitorOptions.MaxTimeoutMs,
            ProbesPerMeasurement = MonitorOptions.MaxProbes,
            MaxCycles = MonitorOptions.MaxMaxCycles
        };
        options.Validate();
    }

    // --- Default values ---

    [Fact]
    public void DefaultValues_AreExpected()
    {
        var options = new MonitorOptions();
        Assert.Equal("1.1.1.1", options.Target);
        Assert.Equal(5, options.IntervalSeconds);
        Assert.Equal(3000, options.TimeoutMs);
        Assert.Equal(4, options.ProbesPerMeasurement);
        Assert.Null(options.MaxCycles);
        Assert.False(options.MeasureDns);
        Assert.False(options.MeasureGateway);
        Assert.Null(options.GatewayAddress);
    }
}
