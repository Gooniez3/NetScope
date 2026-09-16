using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Traces the route to a target by sending ICMP echo requests with incrementing TTL values.
/// </summary>
/// <remarks>
/// <b>Implementation approach:</b> Uses <see cref="Ping"/> with incrementing TTL rather than
/// shelling out to <c>tracert</c>/<c>traceroute</c>. This keeps the implementation
/// cross-platform and structured.
/// <para><b>How it works:</b> Each hop sends an ICMP echo with TTL = hop number.
/// The router at that hop returns <see cref="IPStatus.TtlExpired"/>, revealing its address.
/// When the reply comes from the destination itself with <see cref="IPStatus.Success"/>,
/// the trace is complete.</para>
/// <para><b>Cross-platform notes:</b></para>
/// <list type="bullet">
///   <item>Windows: full support.</item>
///   <item>Linux: requires <c>net.ipv4.ping_group_range</c> or <c>CAP_NET_RAW</c>.</item>
///   <item>macOS: full support through .NET runtime.</item>
///   <item>Some routers drop ICMP or do not decrement TTL, causing timeout hops — this is
///         normal traceroute behavior and is represented as a non-responding hop.</item>
/// </list>
/// </remarks>
public sealed class TracerouteService : ITracerouteService
{
    private static readonly byte[] Payload = "NetScope"u8.ToArray();

    public async Task<TracerouteResult> TraceAsync(
        TracerouteOptions options,
        IProgress<TracerouteHop>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        var timestamp = DateTimeOffset.UtcNow;
        var totalSw = Stopwatch.StartNew();

        // Resolve the target address up front so we can detect destination-reached.
        IPAddress? targetAddress;
        try
        {
            var entry = await Dns.GetHostEntryAsync(options.Target, cancellationToken);
            targetAddress = entry.AddressList.FirstOrDefault(a =>
                a.AddressFamily == AddressFamily.InterNetwork) ?? entry.AddressList.FirstOrDefault();
        }
        catch (SocketException ex)
        {
            totalSw.Stop();
            return new TracerouteResult
            {
                Target = options.Target,
                ResolvedAddress = null,
                Hops = [],
                DestinationReached = false,
                TotalDurationMs = totalSw.Elapsed.TotalMilliseconds,
                Timestamp = timestamp,
                ErrorMessage = $"Could not resolve target: {ex.Message}"
            };
        }
        catch (OperationCanceledException)
        {
            totalSw.Stop();
            return new TracerouteResult
            {
                Target = options.Target,
                ResolvedAddress = null,
                Hops = [],
                DestinationReached = false,
                TotalDurationMs = totalSw.Elapsed.TotalMilliseconds,
                Timestamp = timestamp,
                WasCancelled = true
            };
        }

        if (targetAddress is null)
        {
            totalSw.Stop();
            return new TracerouteResult
            {
                Target = options.Target,
                ResolvedAddress = null,
                Hops = [],
                DestinationReached = false,
                TotalDurationMs = totalSw.Elapsed.TotalMilliseconds,
                Timestamp = timestamp,
                ErrorMessage = "DNS resolution returned no addresses."
            };
        }

        var resolvedStr = targetAddress.ToString();
        var hops = new List<TracerouteHop>();
        var destinationReached = false;

        for (var ttl = 1; ttl <= options.MaxHops; ttl++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var hop = await ProbeHopAsync(ttl, targetAddress, resolvedStr, options, cancellationToken);
            hops.Add(hop);
            progress?.Report(hop);

            if (hop.Address == resolvedStr || hop.Status == "Success")
            {
                destinationReached = true;
                break;
            }
        }

        totalSw.Stop();

        return new TracerouteResult
        {
            Target = options.Target,
            ResolvedAddress = resolvedStr,
            Hops = hops,
            DestinationReached = destinationReached,
            TotalDurationMs = totalSw.Elapsed.TotalMilliseconds,
            Timestamp = timestamp,
            WasCancelled = cancellationToken.IsCancellationRequested
        };
    }

    private static async Task<TracerouteHop> ProbeHopAsync(
        int ttl,
        IPAddress targetAddress,
        string resolvedStr,
        TracerouteOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var pingOptions = new System.Net.NetworkInformation.PingOptions(ttl, dontFragment: true);
            var sw = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(targetAddress, options.TimeoutMs, Payload, pingOptions)
                .WaitAsync(cancellationToken);
            sw.Stop();

            if (reply.Status is IPStatus.Success or IPStatus.TtlExpired)
            {
                var address = reply.Address?.ToString();
                string? hostname = null;

                if (options.ResolveHostnames && address is not null)
                {
                    hostname = await TryReverseDnsAsync(address, cancellationToken);
                }

                var rtt = reply.RoundtripTime > 0
                    ? (double)reply.RoundtripTime
                    : sw.Elapsed.TotalMilliseconds;

                var isDestination = address == resolvedStr;
                var status = isDestination ? "Success" : "TtlExpired";

                return new TracerouteHop
                {
                    HopNumber = ttl,
                    Address = address,
                    Hostname = hostname,
                    RoundTripTimeMs = rtt,
                    Responded = true,
                    Status = status
                };
            }

            if (reply.Status == IPStatus.TimedOut)
            {
                return new TracerouteHop
                {
                    HopNumber = ttl,
                    Responded = false,
                    Status = "TimedOut"
                };
            }

            // Other ICMP statuses (DestinationUnreachable, etc.)
            return new TracerouteHop
            {
                HopNumber = ttl,
                Address = reply.Address?.ToString(),
                Responded = true,
                RoundTripTimeMs = reply.RoundtripTime > 0 ? reply.RoundtripTime : null,
                Status = reply.Status.ToString()
            };
        }
        catch (PingException)
        {
            return new TracerouteHop
            {
                HopNumber = ttl,
                Responded = false,
                Status = "PingException"
            };
        }
        catch (OperationCanceledException)
        {
            return new TracerouteHop
            {
                HopNumber = ttl,
                Responded = false,
                Status = "Cancelled"
            };
        }
    }

    private static async Task<string?> TryReverseDnsAsync(
        string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(ipAddress, cancellationToken);
            // Only return the hostname if it's different from the IP address
            return entry.HostName != ipAddress ? entry.HostName : null;
        }
        catch
        {
            return null;
        }
    }
}
