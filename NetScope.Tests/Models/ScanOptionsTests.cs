using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class ScanOptionsTests
{
    [Fact]
    public void Validate_DefaultValues_Accepted()
    {
        var options = new ScanOptions();
        options.Validate();
    }

    [Fact]
    public void Validate_ExplicitValidValues_Accepted()
    {
        var options = new ScanOptions
        {
            Subnet = "192.168.1.0/24",
            TimeoutMs = 1000,
            Concurrency = 64,
            ResolveHostnames = false,
            ResolveMacAddresses = false
        };
        options.Validate();
    }

    // --- Timeout ---

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Validate_TimeoutBelowMinimum_Throws(int timeout)
    {
        var options = new ScanOptions { TimeoutMs = timeout };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    [Fact]
    public void Validate_TimeoutAboveMaximum_Throws()
    {
        var options = new ScanOptions { TimeoutMs = ScanOptions.MaxTimeoutMs + 1 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TimeoutMs", ex.ParamName);
    }

    // --- Concurrency ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ConcurrencyBelowMinimum_Throws(int concurrency)
    {
        var options = new ScanOptions { Concurrency = concurrency };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Concurrency", ex.ParamName);
    }

    [Fact]
    public void Validate_ConcurrencyAboveMaximum_Throws()
    {
        var options = new ScanOptions { Concurrency = ScanOptions.MaxConcurrency + 1 };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("Concurrency", ex.ParamName);
    }

    // --- Boundaries ---

    [Fact]
    public void Validate_MinBoundary_Accepted()
    {
        var options = new ScanOptions
        {
            TimeoutMs = ScanOptions.MinTimeoutMs,
            Concurrency = ScanOptions.MinConcurrency
        };
        options.Validate();
    }

    [Fact]
    public void Validate_MaxBoundary_Accepted()
    {
        var options = new ScanOptions
        {
            TimeoutMs = ScanOptions.MaxTimeoutMs,
            Concurrency = ScanOptions.MaxConcurrency
        };
        options.Validate();
    }
}
