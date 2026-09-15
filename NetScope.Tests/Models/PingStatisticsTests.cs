using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class PingStatisticsTests
{
    // --- Helper to build results ---

    private static PingResult Success(int seq, double rttMs) => new()
    {
        SequenceNumber = seq,
        Timestamp = DateTimeOffset.UtcNow,
        Success = true,
        RoundTripTimeMs = rttMs,
        Status = "Success"
    };

    private static PingResult Failure(int seq) => new()
    {
        SequenceNumber = seq,
        Timestamp = DateTimeOffset.UtcNow,
        Success = false,
        RoundTripTimeMs = null,
        Status = "TimedOut",
        ErrorMessage = "Request timed out."
    };

    // --- Null input ---

    [Fact]
    public void Calculate_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PingStatistics.Calculate(null!));
    }

    // --- Empty results ---

    [Fact]
    public void Calculate_EmptyResults_ZeroSentZeroLoss()
    {
        var stats = PingStatistics.Calculate([]);

        Assert.Equal(0, stats.Sent);
        Assert.Equal(0, stats.Received);
        Assert.Equal(0.0, stats.PacketLossPercent);
        Assert.Null(stats.MinRoundTripMs);
        Assert.Null(stats.MaxRoundTripMs);
        Assert.Null(stats.AvgRoundTripMs);
        Assert.Null(stats.JitterMs);
    }

    // --- All successful ---

    [Fact]
    public void Calculate_AllSuccessful_ZeroPacketLoss()
    {
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Success(2, 20.0),
            Success(3, 30.0),
            Success(4, 40.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(4, stats.Sent);
        Assert.Equal(4, stats.Received);
        Assert.Equal(0.0, stats.PacketLossPercent);
    }

    // --- All failed ---

    [Fact]
    public void Calculate_AllFailed_100PercentLoss()
    {
        var results = new List<PingResult>
        {
            Failure(1),
            Failure(2),
            Failure(3),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(3, stats.Sent);
        Assert.Equal(0, stats.Received);
        Assert.Equal(100.0, stats.PacketLossPercent);
        Assert.Null(stats.MinRoundTripMs);
        Assert.Null(stats.MaxRoundTripMs);
        Assert.Null(stats.AvgRoundTripMs);
        Assert.Null(stats.JitterMs);
    }

    // --- Partial loss ---

    [Fact]
    public void Calculate_PartialLoss_CorrectPercentage()
    {
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Failure(2),
            Success(3, 20.0),
            Failure(4),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(4, stats.Sent);
        Assert.Equal(2, stats.Received);
        Assert.Equal(50.0, stats.PacketLossPercent);
    }

    // --- Single successful probe ---

    [Fact]
    public void Calculate_OneSuccessful_CorrectStats()
    {
        var results = new List<PingResult> { Success(1, 15.5) };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(1, stats.Sent);
        Assert.Equal(1, stats.Received);
        Assert.Equal(0.0, stats.PacketLossPercent);
        Assert.Equal(15.5, stats.MinRoundTripMs);
        Assert.Equal(15.5, stats.MaxRoundTripMs);
        Assert.Equal(15.5, stats.AvgRoundTripMs);
        Assert.Null(stats.JitterMs); // needs >= 2 probes
    }

    // --- Min RTT ---

    [Fact]
    public void Calculate_MinLatency_IsSmallestRtt()
    {
        var results = new List<PingResult>
        {
            Success(1, 25.0),
            Success(2, 5.0),
            Success(3, 15.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(5.0, stats.MinRoundTripMs);
    }

    // --- Max RTT ---

    [Fact]
    public void Calculate_MaxLatency_IsLargestRtt()
    {
        var results = new List<PingResult>
        {
            Success(1, 25.0),
            Success(2, 5.0),
            Success(3, 15.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(25.0, stats.MaxRoundTripMs);
    }

    // --- Average RTT ---

    [Fact]
    public void Calculate_AverageLatency_IsArithmeticMean()
    {
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Success(2, 20.0),
            Success(3, 30.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(20.0, stats.AvgRoundTripMs);
    }

    // --- Jitter ---

    [Fact]
    public void Calculate_Jitter_MeanAbsoluteConsecutiveDifference()
    {
        // RTTs: 10, 20, 15, 25
        // Diffs: |20-10|=10, |15-20|=5, |25-15|=10
        // Jitter: (10+5+10)/3 = 25/3 ≈ 8.333...
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Success(2, 20.0),
            Success(3, 15.0),
            Success(4, 25.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.NotNull(stats.JitterMs);
        Assert.Equal(25.0 / 3.0, stats.JitterMs!.Value, precision: 10);
    }

    [Fact]
    public void Calculate_IdenticalRtts_ZeroJitter()
    {
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Success(2, 10.0),
            Success(3, 10.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(0.0, stats.JitterMs);
    }

    [Fact]
    public void Calculate_TwoProbes_JitterIsSingleDifference()
    {
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Success(2, 18.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(8.0, stats.JitterMs);
    }

    [Fact]
    public void Calculate_SingleProbe_JitterIsNull()
    {
        var results = new List<PingResult> { Success(1, 10.0) };

        var stats = PingStatistics.Calculate(results);

        Assert.Null(stats.JitterMs);
    }

    // --- Mixed successful and failed ---

    [Fact]
    public void Calculate_MixedResults_StatisticsOnlyFromSuccessful()
    {
        // seq 1: 10ms success
        // seq 2: fail
        // seq 3: 30ms success
        // seq 4: fail
        // seq 5: 20ms success
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Failure(2),
            Success(3, 30.0),
            Failure(4),
            Success(5, 20.0),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(5, stats.Sent);
        Assert.Equal(3, stats.Received);
        Assert.Equal(40.0, stats.PacketLossPercent);
        Assert.Equal(10.0, stats.MinRoundTripMs);
        Assert.Equal(30.0, stats.MaxRoundTripMs);
        Assert.Equal(20.0, stats.AvgRoundTripMs);
        // Consecutive successful RTTs: 10, 30, 20
        // Diffs: |30-10|=20, |20-30|=10
        // Jitter: (20+10)/2 = 15
        Assert.Equal(15.0, stats.JitterMs);
    }

    // --- Packet counts ---

    [Fact]
    public void Calculate_LargeResultSet_CorrectCounts()
    {
        var results = new List<PingResult>();
        for (var i = 1; i <= 100; i++)
        {
            results.Add(i % 3 == 0 ? Failure(i) : Success(i, i * 1.0));
        }

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(100, stats.Sent);
        Assert.Equal(67, stats.Received); // 100 - 33 failures (3,6,9...99)
        Assert.Equal(33.0, stats.PacketLossPercent, precision: 1);
    }

    // --- Packet loss precision ---

    [Fact]
    public void Calculate_OneOfThreeLost_CorrectLossPercentage()
    {
        var results = new List<PingResult>
        {
            Success(1, 10.0),
            Failure(2),
            Success(3, 10.0),
        };

        var stats = PingStatistics.Calculate(results);

        // 1/3 = 33.333...%
        Assert.Equal(100.0 / 3.0, stats.PacketLossPercent, precision: 10);
    }

    // --- Sub-millisecond RTTs ---

    [Fact]
    public void Calculate_SubMillisecondRtts_PreservesPrecision()
    {
        var results = new List<PingResult>
        {
            Success(1, 0.123),
            Success(2, 0.456),
            Success(3, 0.789),
        };

        var stats = PingStatistics.Calculate(results);

        Assert.Equal(0.123, stats.MinRoundTripMs);
        Assert.Equal(0.789, stats.MaxRoundTripMs);
        Assert.Equal((0.123 + 0.456 + 0.789) / 3.0, stats.AvgRoundTripMs!.Value, precision: 10);
    }
}
