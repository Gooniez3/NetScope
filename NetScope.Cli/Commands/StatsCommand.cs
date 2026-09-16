using NetScope.Cli;
using NetScope.Core.Models;
using NetScope.Infrastructure.Persistence;

namespace NetScope.Cli.Commands;

internal static class StatsCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var (sessionId, hours) = ParseArgs(args);
        if (hours < 0)
            return 1;

        await using var repo = new SqliteMeasurementRepository();
        await repo.InitializeAsync();

        MeasurementAggregate? agg;
        string label;

        if (sessionId.HasValue)
        {
            agg = await repo.GetSessionAggregateAsync(sessionId.Value);
            label = $"Session #{sessionId.Value}";
        }
        else
        {
            var to = DateTimeOffset.UtcNow;
            var from = to.AddHours(-hours);
            agg = await repo.GetTimeRangeAggregateAsync(from, to);
            label = $"Last {hours} hour(s)";
        }

        if (agg is null)
        {
            Console.WriteLine($"  No data found for: {label}");
            Console.WriteLine("  Run: netscope monitor --save --count 5");
            return 1;
        }

        PrintAggregate(label, agg);
        return 0;
    }

    private static void PrintAggregate(string label, MeasurementAggregate agg)
    {
        Console.WriteLine();
        Console.WriteLine($"  ── Statistics: {label} ──");
        Console.WriteLine();

        Console.WriteLine($"  Measurements:     {agg.TotalMeasurements}");
        Console.WriteLine($"  Connected:        {agg.ConnectedCount}");
        Console.WriteLine($"  Disconnected:     {agg.DisconnectedCount}");

        var uptimeColor = agg.UptimePercent >= 99.0 ? ConsoleColor.Green :
                          agg.UptimePercent >= 90.0 ? ConsoleColor.Yellow :
                          ConsoleColor.Red;
        Console.Write("  Uptime:           ");
        Console.ForegroundColor = uptimeColor;
        Console.WriteLine($"{agg.UptimePercent:F1}%");
        Console.ResetColor();

        Console.WriteLine($"  Avg packet loss:  {agg.AvgPacketLossPercent:F1}%");
        Console.WriteLine();

        if (agg.AvgLatencyMs.HasValue)
        {
            Console.WriteLine($"  Avg latency:      {agg.AvgLatencyMs.Value:F1} ms");
            Console.WriteLine($"  Min latency:      {agg.MinLatencyMs!.Value:F1} ms");
            Console.WriteLine($"  Max latency:      {agg.MaxLatencyMs!.Value:F1} ms");
        }
        else
        {
            Console.WriteLine("  Avg latency:      ---");
        }

        if (agg.AvgJitterMs.HasValue)
            Console.WriteLine($"  Avg jitter:       {agg.AvgJitterMs.Value:F1} ms");
        else
            Console.WriteLine("  Avg jitter:       ---");

        Console.WriteLine();

        if (agg.PeriodStart.HasValue && agg.PeriodEnd.HasValue)
        {
            Console.WriteLine($"  Period:           {agg.PeriodStart.Value.ToLocalTime():yyyy-MM-dd HH:mm} → " +
                              $"{agg.PeriodEnd.Value.ToLocalTime():yyyy-MM-dd HH:mm}");
        }

        Console.WriteLine();
        Console.WriteLine("  Health breakdown:");

        foreach (var (status, count) in agg.HealthBreakdown.OrderBy(kv => kv.Key))
        {
            var pct = agg.TotalMeasurements > 0 ? count / (double)agg.TotalMeasurements * 100 : 0;
            var color = status switch
            {
                NetworkHealthStatus.Healthy => ConsoleColor.Green,
                NetworkHealthStatus.Degraded => ConsoleColor.Yellow,
                NetworkHealthStatus.Unstable => ConsoleColor.Red,
                NetworkHealthStatus.Disconnected => ConsoleColor.DarkRed,
                _ => ConsoleColor.Gray
            };

            Console.Write($"    ");
            Console.ForegroundColor = color;
            Console.Write($"{status,-14}");
            Console.ResetColor();
            Console.WriteLine($" {count,5}  ({pct:F1}%)");
        }

        Console.WriteLine();
    }

    private static (long? SessionId, int Hours) ParseArgs(string[] args)
    {
        long? sessionId = null;
        var hours = 24;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--session" or "-s" when i + 1 < args.Length:
                    if (!CliParse.TryLong(args[++i], "session id", out var id))
                        return (null, -1);
                    if (id < 1)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Session id must be a positive integer.");
                        Console.ResetColor();
                        return (null, -1);
                    }
                    sessionId = id;
                    break;
                case "--hours" or "-h" when i + 1 < args.Length:
                    if (!CliParse.TryInt(args[++i], "hours", out hours))
                        return (null, -1);
                    hours = CliParse.ClampHours(hours);
                    break;
            }
        }

        return (sessionId, hours);
    }
}
