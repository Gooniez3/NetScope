using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetScope.Core.Models;
using NetScope.Core.Networking;
using PingOptions = NetScope.Core.Models.PingOptions;
using SystemPingOptions = System.Net.NetworkInformation.PingOptions;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Executes ICMP echo sessions using <see cref="Ping"/> from
/// <c>System.Net.NetworkInformation</c>.
/// </summary>
/// <remarks>
/// <b>Cross-platform notes:</b>
/// <list type="bullet">
///   <item>Windows, Linux, macOS: ICMP ping is supported via the .NET runtime.</item>
///   <item>Linux: may require <c>net.ipv4.ping_group_range</c> sysctl or <c>CAP_NET_RAW</c>.</item>
///   <item>TTL in replies: available on all platforms, but the value reported by the OS
///         can differ in edge cases (e.g. some Linux kernels return 0 for localhost).</item>
///   <item>Payload: the buffer is sent as-is. Very large payloads may be rejected by
///         intermediate routers or the OS — this is surfaced as a non-success result,
///         not an exception.</item>
/// </list>
/// </remarks>
public sealed class PingService : IPingService
{
    public async Task<(IReadOnlyList<PingResult> Results, PingStatistics Statistics)> PingAsync(
        PingOptions options,
        IProgress<PingResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        var results = new List<PingResult>(options.Count);
        var payload = CreatePayload(options.PayloadSize);
        var systemOptions = CreateSystemOptions(options);

        for (var seq = 1; seq <= options.Count; seq++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            PingResult result;
            try
            {
                result = await SendProbeAsync(
                    options.Target, seq, options.TimeoutMs, payload, systemOptions, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            results.Add(result);
            progress?.Report(result);

            // Wait for the configured interval before the next probe, unless this is the last one.
            if (seq < options.Count && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(options.IntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        var statistics = PingStatistics.Calculate(results);
        return (results, statistics);
    }

    private static async Task<PingResult> SendProbeAsync(
        string target,
        int sequenceNumber,
        int timeoutMs,
        byte[] payload,
        SystemPingOptions systemOptions,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;

        try
        {
            using var ping = new Ping();
            var sw = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(target, timeoutMs, payload, systemOptions)
                .WaitAsync(cancellationToken);
            sw.Stop();

            if (reply.Status == IPStatus.Success)
            {
                // Use Stopwatch RTT when the OS reports 0 ms (sub-millisecond replies).
                var rtt = reply.RoundtripTime > 0
                    ? (double)reply.RoundtripTime
                    : sw.Elapsed.TotalMilliseconds;

                return new PingResult
                {
                    SequenceNumber = sequenceNumber,
                    Timestamp = timestamp,
                    Success = true,
                    RoundTripTimeMs = rtt,
                    Status = nameof(IPStatus.Success),
                    ReplyTtl = reply.Options?.Ttl,
                    ReplyBufferSize = reply.Buffer?.Length
                };
            }

            return new PingResult
            {
                SequenceNumber = sequenceNumber,
                Timestamp = timestamp,
                Success = false,
                RoundTripTimeMs = null,
                Status = reply.Status.ToString(),
                ErrorMessage = MapStatusToMessage(reply.Status),
                ReplyTtl = reply.Options?.Ttl
            };
        }
        catch (PingException ex)
        {
            return new PingResult
            {
                SequenceNumber = sequenceNumber,
                Timestamp = timestamp,
                Success = false,
                RoundTripTimeMs = null,
                Status = "PingException",
                ErrorMessage = ex.InnerException?.Message ?? ex.Message
            };
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException)
        {
            return new PingResult
            {
                SequenceNumber = sequenceNumber,
                Timestamp = timestamp,
                Success = false,
                RoundTripTimeMs = null,
                Status = ex.GetType().Name,
                ErrorMessage = ex.Message
            };
        }
    }

    private static SystemPingOptions CreateSystemOptions(PingOptions options)
    {
        var sysOpts = new SystemPingOptions();
        if (options.Ttl.HasValue)
            sysOpts.Ttl = options.Ttl.Value;
        sysOpts.DontFragment = false;
        return sysOpts;
    }

    private static byte[] CreatePayload(int size)
    {
        if (size == 0) return [];

        var buffer = new byte[size];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = (byte)(0x41 + (i % 26)); // A-Z repeating pattern
        return buffer;
    }

    private static string MapStatusToMessage(IPStatus status) => status switch
    {
        IPStatus.TimedOut => "Request timed out.",
        IPStatus.DestinationHostUnreachable => "Destination host unreachable.",
        IPStatus.DestinationNetworkUnreachable => "Destination network unreachable.",
        IPStatus.DestinationPortUnreachable => "Destination port unreachable.",
        IPStatus.DestinationUnreachable => "Destination unreachable.",
        IPStatus.TtlExpired => "TTL expired in transit.",
        IPStatus.BadRoute => "Bad route.",
        IPStatus.HardwareError => "Hardware error.",
        IPStatus.PacketTooBig => "Packet too big.",
        IPStatus.BadDestination => "Bad destination.",
        IPStatus.BadHeader => "Bad header.",
        _ => $"ICMP error: {status}."
    };
}
