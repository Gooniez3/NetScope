namespace NetScope.Core.Models;

/// <summary>
/// Aggregate statistics computed from a sequence of <see cref="PingResult"/> probes.
/// </summary>
/// <remarks>
/// <para><b>Jitter calculation:</b> Uses the mean of absolute differences between
/// consecutive successful round-trip times (RFC 3550 interarrival jitter concept,
/// simplified). Specifically:</para>
/// <code>
///   jitter = mean(|RTT[i] − RTT[i−1]|) for i = 1..N−1
/// </code>
/// <para>This measures how much latency varies from one probe to the next. A jitter
/// of 0 means every probe had exactly the same RTT. Higher values indicate less
/// stable connectivity. Requires at least 2 successful probes to compute.</para>
/// </remarks>
public sealed class PingStatistics
{
    /// <summary>Total number of probes sent.</summary>
    public required int Sent { get; init; }

    /// <summary>Number of probes that received a successful reply.</summary>
    public required int Received { get; init; }

    /// <summary>Percentage of probes that did not receive a reply (0.0–100.0).</summary>
    public required double PacketLossPercent { get; init; }

    /// <summary>Minimum round-trip time in ms among successful probes, or null if none succeeded.</summary>
    public double? MinRoundTripMs { get; init; }

    /// <summary>Maximum round-trip time in ms among successful probes, or null if none succeeded.</summary>
    public double? MaxRoundTripMs { get; init; }

    /// <summary>Arithmetic mean round-trip time in ms among successful probes, or null if none succeeded.</summary>
    public double? AvgRoundTripMs { get; init; }

    /// <summary>
    /// Mean of absolute consecutive RTT differences in ms, or null if fewer than 2 probes succeeded.
    /// See class remarks for the formula.
    /// </summary>
    public double? JitterMs { get; init; }

    /// <summary>
    /// Computes statistics from a completed set of ping results.
    /// This is a pure function — no I/O, no side effects — and is fully unit-testable.
    /// </summary>
    public static PingStatistics Calculate(IReadOnlyList<PingResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var sent = results.Count;
        var successful = results.Where(r => r.Success && r.RoundTripTimeMs.HasValue).ToList();
        var received = successful.Count;
        var lossPercent = sent > 0 ? (sent - received) / (double)sent * 100.0 : 0.0;

        if (received == 0)
        {
            return new PingStatistics
            {
                Sent = sent,
                Received = 0,
                PacketLossPercent = sent > 0 ? 100.0 : 0.0,
                MinRoundTripMs = null,
                MaxRoundTripMs = null,
                AvgRoundTripMs = null,
                JitterMs = null
            };
        }

        var rtts = successful.Select(r => r.RoundTripTimeMs!.Value).ToList();

        double? jitter = null;
        if (rtts.Count >= 2)
        {
            var sumAbsDiffs = 0.0;
            for (var i = 1; i < rtts.Count; i++)
            {
                sumAbsDiffs += Math.Abs(rtts[i] - rtts[i - 1]);
            }
            jitter = sumAbsDiffs / (rtts.Count - 1);
        }

        return new PingStatistics
        {
            Sent = sent,
            Received = received,
            PacketLossPercent = lossPercent,
            MinRoundTripMs = rtts.Min(),
            MaxRoundTripMs = rtts.Max(),
            AvgRoundTripMs = rtts.Average(),
            JitterMs = jitter
        };
    }
}
