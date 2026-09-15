using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class TracerouteResultTests
{
    // --- Hop ordering ---

    [Fact]
    public void Hops_MaintainOrder()
    {
        var hops = new List<TracerouteHop>
        {
            new() { HopNumber = 1, Responded = true, Status = "TtlExpired", Address = "192.168.1.1", RoundTripTimeMs = 1.0 },
            new() { HopNumber = 2, Responded = true, Status = "TtlExpired", Address = "10.0.0.1", RoundTripTimeMs = 5.0 },
            new() { HopNumber = 3, Responded = true, Status = "Success", Address = "8.8.8.8", RoundTripTimeMs = 10.0 },
        };

        var result = new TracerouteResult
        {
            Target = "8.8.8.8",
            ResolvedAddress = "8.8.8.8",
            Hops = hops,
            DestinationReached = true,
            TotalDurationMs = 100,
            Timestamp = DateTimeOffset.UtcNow,
        };

        Assert.Equal(3, result.Hops.Count);
        Assert.Equal(1, result.Hops[0].HopNumber);
        Assert.Equal(2, result.Hops[1].HopNumber);
        Assert.Equal(3, result.Hops[2].HopNumber);
    }

    // --- Destination reached ---

    [Fact]
    public void DestinationReached_TrueWhenFinalHopIsTarget()
    {
        var result = new TracerouteResult
        {
            Target = "example.com",
            ResolvedAddress = "93.184.216.34",
            Hops =
            [
                new() { HopNumber = 1, Responded = true, Status = "TtlExpired", Address = "192.168.1.1", RoundTripTimeMs = 1 },
                new() { HopNumber = 2, Responded = true, Status = "Success", Address = "93.184.216.34", RoundTripTimeMs = 20 },
            ],
            DestinationReached = true,
            TotalDurationMs = 50,
            Timestamp = DateTimeOffset.UtcNow,
        };

        Assert.True(result.DestinationReached);
    }

    // --- Destination not reached ---

    [Fact]
    public void DestinationNotReached_AllHopsTimeout()
    {
        var hops = Enumerable.Range(1, 5)
            .Select(i => new TracerouteHop { HopNumber = i, Responded = false, Status = "TimedOut" })
            .ToList();

        var result = new TracerouteResult
        {
            Target = "unreachable.test",
            Hops = hops,
            DestinationReached = false,
            TotalDurationMs = 15000,
            Timestamp = DateTimeOffset.UtcNow,
        };

        Assert.False(result.DestinationReached);
        Assert.All(result.Hops, h => Assert.False(h.Responded));
    }

    // --- Timeout hop properties ---

    [Fact]
    public void TimeoutHop_HasNoAddressOrRtt()
    {
        var hop = new TracerouteHop
        {
            HopNumber = 3,
            Responded = false,
            Status = "TimedOut"
        };

        Assert.Null(hop.Address);
        Assert.Null(hop.Hostname);
        Assert.Null(hop.RoundTripTimeMs);
        Assert.False(hop.Responded);
    }

    // --- Responding hop properties ---

    [Fact]
    public void RespondingHop_HasAddressAndRtt()
    {
        var hop = new TracerouteHop
        {
            HopNumber = 2,
            Address = "10.0.0.1",
            Hostname = "router.local",
            RoundTripTimeMs = 3.5,
            Responded = true,
            Status = "TtlExpired"
        };

        Assert.NotNull(hop.Address);
        Assert.NotNull(hop.Hostname);
        Assert.NotNull(hop.RoundTripTimeMs);
        Assert.True(hop.Responded);
    }

    // --- Cancellation state ---

    [Fact]
    public void CancelledResult_HasPartialHops()
    {
        var result = new TracerouteResult
        {
            Target = "example.com",
            ResolvedAddress = "93.184.216.34",
            Hops =
            [
                new() { HopNumber = 1, Responded = true, Status = "TtlExpired", Address = "192.168.1.1", RoundTripTimeMs = 1 },
            ],
            DestinationReached = false,
            TotalDurationMs = 500,
            Timestamp = DateTimeOffset.UtcNow,
            WasCancelled = true,
        };

        Assert.True(result.WasCancelled);
        Assert.False(result.DestinationReached);
        Assert.Single(result.Hops);
    }

    // --- Error result ---

    [Fact]
    public void ErrorResult_ResolutionFailure()
    {
        var result = new TracerouteResult
        {
            Target = "invalid.host.test",
            Hops = [],
            DestinationReached = false,
            TotalDurationMs = 10,
            Timestamp = DateTimeOffset.UtcNow,
            ErrorMessage = "Could not resolve target: No such host is known."
        };

        Assert.False(result.DestinationReached);
        Assert.Empty(result.Hops);
        Assert.NotNull(result.ErrorMessage);
    }

    // --- Mixed hops (some respond, some timeout) ---

    [Fact]
    public void MixedHops_RespondingAndTimeout()
    {
        var result = new TracerouteResult
        {
            Target = "example.com",
            ResolvedAddress = "93.184.216.34",
            Hops =
            [
                new() { HopNumber = 1, Responded = true, Status = "TtlExpired", Address = "192.168.1.1", RoundTripTimeMs = 1 },
                new() { HopNumber = 2, Responded = false, Status = "TimedOut" },
                new() { HopNumber = 3, Responded = false, Status = "TimedOut" },
                new() { HopNumber = 4, Responded = true, Status = "Success", Address = "93.184.216.34", RoundTripTimeMs = 25 },
            ],
            DestinationReached = true,
            TotalDurationMs = 5000,
            Timestamp = DateTimeOffset.UtcNow,
        };

        Assert.Equal(4, result.Hops.Count);
        Assert.Equal(2, result.Hops.Count(h => h.Responded));
        Assert.Equal(2, result.Hops.Count(h => !h.Responded));
        Assert.True(result.DestinationReached);
    }
}
