using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using NetScope.Core.Models;
using NetScope.Core.Networking;
using PingOptions = NetScope.Core.Models.PingOptions;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Continuously monitors network health by composing <see cref="IPingService"/>
/// and optionally <see cref="IDnsService"/> into periodic measurement cycles.
/// </summary>
/// <remarks>
/// <para>Each cycle runs a short ping session (N probes, fast interval),
/// then optionally measures DNS resolution and gateway latency.</para>
/// <para>If a cycle takes longer than the configured interval, the next cycle
/// starts immediately — cycles never overlap.</para>
/// </remarks>
public sealed class NetworkMonitorService : INetworkMonitorService
{
    private readonly IPingService _pingService;
    private readonly IDnsService? _dnsService;
    private readonly INetworkInterfaceService? _interfaceService;

    public NetworkMonitorService(
        IPingService pingService,
        IDnsService? dnsService = null,
        INetworkInterfaceService? interfaceService = null)
    {
        _pingService = pingService;
        _dnsService = dnsService;
        _interfaceService = interfaceService;
    }

    public async IAsyncEnumerable<NetworkMeasurement> MonitorAsync(
        MonitorOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options.Validate();

        var gatewayAddress = ResolveGateway(options);
        var cycle = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            cycle++;

            if (options.MaxCycles.HasValue && cycle > options.MaxCycles.Value)
                yield break;

            var cycleSw = Stopwatch.StartNew();

            var measurement = await MeasureCycleAsync(
                cycle, options, gatewayAddress, cancellationToken);

            yield return measurement;

            cycleSw.Stop();

            // Check again after yielding — caller may have cancelled
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (options.MaxCycles.HasValue && cycle >= options.MaxCycles.Value)
                yield break;

            // Wait for the remaining interval (skip if cycle took longer)
            var elapsed = cycleSw.Elapsed;
            var interval = TimeSpan.FromSeconds(options.IntervalSeconds);
            var remaining = interval - elapsed;

            if (remaining > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(remaining, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
            }
        }
    }

    private async Task<NetworkMeasurement> MeasureCycleAsync(
        int cycleNumber,
        MonitorOptions options,
        string? gatewayAddress,
        CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow;

        // Primary ping measurement — fast probes within the cycle
        var pingOptions = new PingOptions
        {
            Target = options.Target,
            Count = options.ProbesPerMeasurement,
            IntervalMs = 200, // fast within-cycle probing
            TimeoutMs = options.TimeoutMs,
            PayloadSize = 32
        };

        var (_, stats) = await _pingService.PingAsync(pingOptions, cancellationToken: ct);

        // Optional DNS measurement
        double? dnsMs = null;
        if (options.MeasureDns && _dnsService is not null)
        {
            try
            {
                var dnsResult = await _dnsService.ResolveAsync(options.Target, ct);
                dnsMs = dnsResult.ResolutionMs;
            }
            catch
            {
                // DNS measurement failure is non-fatal
            }
        }

        // Optional gateway measurement
        double? gatewayMs = null;
        if (options.MeasureGateway && gatewayAddress is not null)
        {
            gatewayMs = await MeasureGatewayAsync(gatewayAddress, options.TimeoutMs, ct);
        }

        var health = HealthClassifier.Classify(
            stats.PacketLossPercent, stats.AvgRoundTripMs, stats.JitterMs);

        return new NetworkMeasurement
        {
            Timestamp = timestamp,
            CycleNumber = cycleNumber,
            Target = options.Target,
            IsConnected = stats.Received > 0,
            PacketsSent = stats.Sent,
            PacketsReceived = stats.Received,
            PacketLossPercent = stats.PacketLossPercent,
            MinLatencyMs = stats.MinRoundTripMs,
            MaxLatencyMs = stats.MaxRoundTripMs,
            AvgLatencyMs = stats.AvgRoundTripMs,
            JitterMs = stats.JitterMs,
            DnsResolutionMs = dnsMs,
            GatewayLatencyMs = gatewayMs,
            HealthStatus = health
        };
    }

    private static async Task<double?> MeasureGatewayAsync(
        string gateway, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            var sw = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(gateway, timeoutMs).WaitAsync(ct);
            sw.Stop();

            if (reply.Status == IPStatus.Success)
            {
                return reply.RoundtripTime > 0
                    ? reply.RoundtripTime
                    : sw.Elapsed.TotalMilliseconds;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveGateway(MonitorOptions options)
    {
        if (!options.MeasureGateway)
            return null;

        if (options.GatewayAddress is not null)
            return options.GatewayAddress;

        // Auto-detect from active interface
        var active = _interfaceService?.GetActiveInterface();
        return active?.GatewayAddresses.FirstOrDefault();
    }
}
