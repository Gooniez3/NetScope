using System.Diagnostics;
using System.Net.NetworkInformation;
using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Tests internet connectivity by sending an ICMP echo (ping) to a well-known target.
/// </summary>
public sealed class ConnectionTestService : IConnectionTestService
{
    private static readonly string[] DefaultTargets = ["1.1.1.1", "8.8.8.8", "9.9.9.9"];

    public async Task<ConnectionStatus> TestConnectionAsync(
        string? target = null,
        int timeoutMs = 3000,
        CancellationToken cancellationToken = default)
    {
        var probeTarget = target ?? DefaultTargets[0];

        try
        {
            using var ping = new Ping();
            var sw = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(probeTarget, timeoutMs);
            sw.Stop();

            if (reply.Status == IPStatus.Success)
            {
                return new ConnectionStatus
                {
                    IsConnected = true,
                    LatencyMs = reply.RoundtripTime > 0 ? reply.RoundtripTime : sw.Elapsed.TotalMilliseconds,
                    ProbeTarget = probeTarget,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }

            // First target failed — try fallbacks if using defaults
            if (target is null)
            {
                return await TryFallbacksAsync(timeoutMs, cancellationToken);
            }

            return Disconnected(probeTarget);
        }
        catch (PingException)
        {
            if (target is null)
            {
                return await TryFallbacksAsync(timeoutMs, cancellationToken);
            }
            return Disconnected(probeTarget);
        }
    }

    private async Task<ConnectionStatus> TryFallbacksAsync(int timeoutMs, CancellationToken ct)
    {
        foreach (var fallback in DefaultTargets.Skip(1))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(fallback, timeoutMs);

                if (reply.Status == IPStatus.Success)
                {
                    return new ConnectionStatus
                    {
                        IsConnected = true,
                        LatencyMs = reply.RoundtripTime,
                        ProbeTarget = fallback,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }
            }
            catch (PingException)
            {
                // Try next fallback
            }
        }

        return Disconnected(DefaultTargets[0]);
    }

    private static ConnectionStatus Disconnected(string target) => new()
    {
        IsConnected = false,
        LatencyMs = null,
        ProbeTarget = target,
        Timestamp = DateTimeOffset.UtcNow
    };
}
