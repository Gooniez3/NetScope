using NetScope.Core.Models;
using NetScope.Core.Persistence;
using NetScope.Infrastructure.Network;
using NetScope.Infrastructure.Persistence;

namespace NetScope.Cli.Commands;

internal static class MonitorCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var (options, save) = ParseArgs(args);
        options.Validate();

        var pingService = new PingService();
        var dnsService = new DnsService();
        var interfaceService = new NetworkInterfaceService();
        var monitor = new NetworkMonitorService(pingService, dnsService, interfaceService);

        IMeasurementRepository? repo = null;
        long sessionId = 0;

        if (save)
        {
            repo = new SqliteMeasurementRepository();
            await repo.InitializeAsync();
            var session = await repo.CreateSessionAsync(new MonitoringSession
            {
                Id = 0,
                StartedAt = DateTimeOffset.UtcNow,
                Target = options.Target,
                IntervalSeconds = options.IntervalSeconds,
                ProbesPerMeasurement = options.ProbesPerMeasurement,
                TimeoutMs = options.TimeoutMs
            });
            sessionId = session.Id;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        PrintHeader(options, save);
        PrintTableHeader();

        var count = 0;
        var cancelled = false;

        try
        {
            await foreach (var m in monitor.MonitorAsync(options, cts.Token))
            {
                count++;
                PrintMeasurement(m);

                if (repo is not null)
                {
                    try
                    {
                        await repo.SaveMeasurementAsync(sessionId, m);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"  [warn] Failed to save measurement: {ex.Message}");
                        Console.ResetColor();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        // Complete session
        if (repo is not null)
        {
            try
            {
                await repo.CompleteSessionAsync(
                    sessionId, DateTimeOffset.UtcNow, count, !cancelled);
            }
            catch
            {
                // Best-effort session completion
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  Monitoring complete. {count} measurement(s) collected.");
        if (save)
            Console.WriteLine($"  Session #{sessionId} saved to database.");

        if (repo is not null)
            await repo.DisposeAsync();

        return 0;
    }

    private static (MonitorOptions Options, bool Save) ParseArgs(string[] args)
    {
        var target = "1.1.1.1";
        var intervalSeconds = 5;
        var timeoutMs = 3000;
        var probes = 4;
        int? maxCycles = null;
        var measureDns = false;
        var measureGateway = false;
        string? gatewayAddress = null;
        var save = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--target" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                case "--interval" or "-i" when i + 1 < args.Length:
                    intervalSeconds = int.Parse(args[++i]);
                    break;
                case "--timeout" or "-t" when i + 1 < args.Length:
                    timeoutMs = int.Parse(args[++i]);
                    break;
                case "--probes" or "-p" when i + 1 < args.Length:
                    probes = int.Parse(args[++i]);
                    break;
                case "--count" or "-c" when i + 1 < args.Length:
                    maxCycles = int.Parse(args[++i]);
                    break;
                case "--dns":
                    measureDns = true;
                    break;
                case "--gateway":
                    measureGateway = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                        gatewayAddress = args[++i];
                    break;
                case "--save":
                    save = true;
                    break;
                default:
                    if (!args[i].StartsWith('-') && i == 0)
                        target = args[i];
                    break;
            }
        }

        var options = new MonitorOptions
        {
            Target = target,
            IntervalSeconds = intervalSeconds,
            TimeoutMs = timeoutMs,
            ProbesPerMeasurement = probes,
            MaxCycles = maxCycles,
            MeasureDns = measureDns,
            MeasureGateway = measureGateway,
            GatewayAddress = gatewayAddress
        };

        return (options, save);
    }

    private static void PrintHeader(MonitorOptions options, bool save)
    {
        Console.WriteLine();
        Console.WriteLine($"  Monitoring {options.Target} every {options.IntervalSeconds}s " +
            $"({options.ProbesPerMeasurement} probes/cycle, {options.TimeoutMs}ms timeout)");

        if (options.MaxCycles.HasValue)
            Console.WriteLine($"  Cycles: {options.MaxCycles.Value}");
        else
            Console.WriteLine("  Cycles: unlimited (Ctrl+C to stop)");

        if (save)
            Console.WriteLine("  Persistence: enabled (SQLite)");

        Console.WriteLine();
    }

    private static void PrintTableHeader()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  TIME       TARGET          LATENCY    LOSS     JITTER    STATUS");
        Console.WriteLine("  ─────────  ──────────────  ─────────  ───────  ────────  ────────────");
        Console.ResetColor();
    }

    private static void PrintMeasurement(NetworkMeasurement m)
    {
        var time = m.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        var target = m.Target.Length > 14 ? m.Target[..14] : m.Target.PadRight(14);
        var latency = m.AvgLatencyMs.HasValue ? $"{m.AvgLatencyMs.Value,6:F1} ms" : "     --- ";
        var loss = $"{m.PacketLossPercent,4:F0}%   ";
        var jitter = m.JitterMs.HasValue ? $"{m.JitterMs.Value,5:F1} ms" : "   ---  ";
        var status = m.HealthStatus.ToString();

        var color = m.HealthStatus switch
        {
            NetworkHealthStatus.Healthy => ConsoleColor.Green,
            NetworkHealthStatus.Degraded => ConsoleColor.Yellow,
            NetworkHealthStatus.Unstable => ConsoleColor.Red,
            NetworkHealthStatus.Disconnected => ConsoleColor.DarkRed,
            _ => ConsoleColor.Gray
        };

        Console.Write($"  {time}   {target}  {latency}  {loss}  {jitter}  ");
        Console.ForegroundColor = color;
        Console.WriteLine(status);
        Console.ResetColor();
    }
}
