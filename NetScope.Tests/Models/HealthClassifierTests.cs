using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class HealthClassifierTests
{
    // --- Healthy ---

    [Fact]
    public void Classify_ZeroLossLowLatencyLowJitter_ReturnsHealthy()
    {
        var result = HealthClassifier.Classify(0.0, 5.0, 1.0);
        Assert.Equal(NetworkHealthStatus.Healthy, result);
    }

    [Fact]
    public void Classify_ZeroLossNullJitter_ReturnsHealthy()
    {
        var result = HealthClassifier.Classify(0.0, 10.0, null);
        Assert.Equal(NetworkHealthStatus.Healthy, result);
    }

    [Fact]
    public void Classify_ZeroLossNullLatencyNullJitter_ReturnsHealthy()
    {
        // Edge case: no RTT data but no loss — treated as healthy
        // This shouldn't normally happen but the classifier handles it
        var result = HealthClassifier.Classify(0.0, null, null);
        Assert.Equal(NetworkHealthStatus.Healthy, result);
    }

    [Fact]
    public void Classify_ZeroLossLatencyJustBelow100_ReturnsHealthy()
    {
        var result = HealthClassifier.Classify(0.0, 99.9, 9.9);
        Assert.Equal(NetworkHealthStatus.Healthy, result);
    }

    // --- Degraded ---

    [Fact]
    public void Classify_SmallPacketLoss_ReturnsDegraded()
    {
        var result = HealthClassifier.Classify(5.0, 20.0, 3.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    [Fact]
    public void Classify_HighLatencyZeroLoss_ReturnsDegraded()
    {
        var result = HealthClassifier.Classify(0.0, 150.0, 2.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    [Fact]
    public void Classify_HighJitterZeroLoss_ReturnsDegraded()
    {
        var result = HealthClassifier.Classify(0.0, 20.0, 15.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    [Fact]
    public void Classify_Latency100Exactly_ReturnsDegraded()
    {
        var result = HealthClassifier.Classify(0.0, 100.0, 0.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    [Fact]
    public void Classify_Jitter10Exactly_ReturnsDegraded()
    {
        var result = HealthClassifier.Classify(0.0, 50.0, 10.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    [Fact]
    public void Classify_LossJustAboveZero_ReturnsDegraded()
    {
        var result = HealthClassifier.Classify(0.1, 5.0, 1.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    [Fact]
    public void Classify_LossBelow10_ReturnsDegraded()
    {
        var result = HealthClassifier.Classify(9.9, 5.0, 1.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    // --- Unstable ---

    [Fact]
    public void Classify_HighPacketLoss_ReturnsUnstable()
    {
        var result = HealthClassifier.Classify(25.0, 50.0, 5.0);
        Assert.Equal(NetworkHealthStatus.Unstable, result);
    }

    [Fact]
    public void Classify_VeryHighLatency_ReturnsUnstable()
    {
        var result = HealthClassifier.Classify(0.0, 600.0, 5.0);
        Assert.Equal(NetworkHealthStatus.Unstable, result);
    }

    [Fact]
    public void Classify_VeryHighJitter_ReturnsUnstable()
    {
        var result = HealthClassifier.Classify(0.0, 50.0, 60.0);
        Assert.Equal(NetworkHealthStatus.Unstable, result);
    }

    [Fact]
    public void Classify_Loss10Exactly_ReturnsUnstable()
    {
        var result = HealthClassifier.Classify(10.0, 5.0, 1.0);
        Assert.Equal(NetworkHealthStatus.Unstable, result);
    }

    [Fact]
    public void Classify_Latency501_ReturnsUnstable()
    {
        var result = HealthClassifier.Classify(0.0, 501.0, 1.0);
        Assert.Equal(NetworkHealthStatus.Unstable, result);
    }

    [Fact]
    public void Classify_Jitter51_ReturnsUnstable()
    {
        var result = HealthClassifier.Classify(0.0, 10.0, 51.0);
        Assert.Equal(NetworkHealthStatus.Unstable, result);
    }

    [Fact]
    public void Classify_Loss99_ReturnsUnstable()
    {
        var result = HealthClassifier.Classify(99.0, null, null);
        Assert.Equal(NetworkHealthStatus.Unstable, result);
    }

    // --- Disconnected ---

    [Fact]
    public void Classify_Loss100_ReturnsDisconnected()
    {
        var result = HealthClassifier.Classify(100.0, null, null);
        Assert.Equal(NetworkHealthStatus.Disconnected, result);
    }

    [Fact]
    public void Classify_Loss100WithZeroLatency_ReturnsDisconnected()
    {
        var result = HealthClassifier.Classify(100.0, 0.0, 0.0);
        Assert.Equal(NetworkHealthStatus.Disconnected, result);
    }

    // --- Boundary transitions ---

    [Fact]
    public void Classify_Latency500Exactly_ReturnsDegraded()
    {
        // 500 is not > 500, so it's not Unstable
        var result = HealthClassifier.Classify(0.0, 500.0, 0.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }

    [Fact]
    public void Classify_Jitter50Exactly_ReturnsDegraded()
    {
        // 50 is not > 50, so it's not Unstable
        var result = HealthClassifier.Classify(0.0, 10.0, 50.0);
        Assert.Equal(NetworkHealthStatus.Degraded, result);
    }
}
