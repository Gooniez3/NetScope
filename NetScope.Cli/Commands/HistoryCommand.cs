using NetScope.Cli;
using NetScope.Core.Models;
using NetScope.Infrastructure.Persistence;

namespace NetScope.Cli.Commands;

internal static class HistoryCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var (mode, sessionId, limit, olderThanDays) = ParseArgs(args);
        if (limit < 0)
            return 1;

        await using var repo = new SqliteMeasurementRepository();
        await repo.InitializeAsync();

        return mode switch
        {
            "sessions" => await ShowSessions(repo, limit),
            "session" when sessionId.HasValue => await ShowSessionDetail(repo, sessionId.Value),
            "recent" => await ShowRecentMeasurements(repo, limit),
            "cleanup" => await RunCleanup(repo, olderThanDays),
            _ => await ShowSessions(repo, limit)
        };
    }

    private static async Task<int> ShowSessions(SqliteMeasurementRepository repo, int limit)
    {
        var sessions = await repo.GetRecentSessionsAsync(limit);

        if (sessions.Count == 0)
        {
            Console.WriteLine("  No monitoring sessions found.");
            Console.WriteLine("  Run: netscope monitor --save --count 5");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine($"  Recent monitoring sessions ({sessions.Count}):");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  ID    STARTED              TARGET          CYCLES  STATUS");
        Console.WriteLine("  ────  ───────────────────  ──────────────  ──────  ──────────");
        Console.ResetColor();

        foreach (var s in sessions)
        {
            var started = s.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var target = s.Target.Length > 14 ? s.Target[..14] : s.Target.PadRight(14);
            var cycles = s.MeasurementCount.ToString().PadLeft(6);
            var status = s.CompletedNormally ? "Complete" :
                         s.EndedAt.HasValue ? "Cancelled" : "Running";

            var color = s.CompletedNormally ? ConsoleColor.Green :
                        s.EndedAt.HasValue ? ConsoleColor.Yellow : ConsoleColor.Cyan;

            Console.Write($"  {s.Id,-4}  {started}  {target}  {cycles}  ");
            Console.ForegroundColor = color;
            Console.WriteLine(status);
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("  View session: netscope history --session <id>");
        return 0;
    }

    private static async Task<int> ShowSessionDetail(SqliteMeasurementRepository repo, long sessionId)
    {
        var measurements = await repo.GetMeasurementsBySessionAsync(sessionId);

        if (measurements.Count == 0)
        {
            Console.WriteLine($"  No measurements found for session #{sessionId}.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"  Session #{sessionId} — {measurements.Count} measurement(s):");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  #     TIME       TARGET          LATENCY    LOSS     JITTER    STATUS");
        Console.WriteLine("  ────  ─────────  ──────────────  ─────────  ───────  ────────  ────────────");
        Console.ResetColor();

        foreach (var m in measurements)
        {
            var cycle = m.CycleNumber.ToString().PadLeft(4);
            var time = m.Timestamp.ToLocalTime().ToString("HH:mm:ss");
            var target = m.Target.Length > 14 ? m.Target[..14] : m.Target.PadRight(14);
            var latency = m.AvgLatencyMs.HasValue ? $"{m.AvgLatencyMs.Value,6:F1} ms" : "     --- ";
            var loss = $"{m.PacketLossPercent,4:F0}%   ";
            var jitter = m.JitterMs.HasValue ? $"{m.JitterMs.Value,5:F1} ms" : "   ---  ";

            var color = m.HealthStatus switch
            {
                NetworkHealthStatus.Healthy => ConsoleColor.Green,
                NetworkHealthStatus.Degraded => ConsoleColor.Yellow,
                NetworkHealthStatus.Unstable => ConsoleColor.Red,
                NetworkHealthStatus.Disconnected => ConsoleColor.DarkRed,
                _ => ConsoleColor.Gray
            };

            Console.Write($"  {cycle}  {time}   {target}  {latency}  {loss}  {jitter}  ");
            Console.ForegroundColor = color;
            Console.WriteLine(m.HealthStatus);
            Console.ResetColor();
        }

        Console.WriteLine();
        return 0;
    }

    private static async Task<int> ShowRecentMeasurements(SqliteMeasurementRepository repo, int limit)
    {
        var measurements = await repo.GetRecentMeasurementsAsync(limit);

        if (measurements.Count == 0)
        {
            Console.WriteLine("  No measurements found.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine($"  Recent measurements ({measurements.Count}):");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  TIME                TARGET          LATENCY    LOSS     STATUS");
        Console.WriteLine("  ──────────────────  ──────────────  ─────────  ───────  ────────────");
        Console.ResetColor();

        foreach (var m in measurements)
        {
            var time = m.Timestamp.ToLocalTime().ToString("MM-dd HH:mm:ss");
            var target = m.Target.Length > 14 ? m.Target[..14] : m.Target.PadRight(14);
            var latency = m.AvgLatencyMs.HasValue ? $"{m.AvgLatencyMs.Value,6:F1} ms" : "     --- ";
            var loss = $"{m.PacketLossPercent,4:F0}%   ";

            var color = m.HealthStatus switch
            {
                NetworkHealthStatus.Healthy => ConsoleColor.Green,
                NetworkHealthStatus.Degraded => ConsoleColor.Yellow,
                NetworkHealthStatus.Unstable => ConsoleColor.Red,
                NetworkHealthStatus.Disconnected => ConsoleColor.DarkRed,
                _ => ConsoleColor.Gray
            };

            Console.Write($"  {time}  {target}  {latency}  {loss}  ");
            Console.ForegroundColor = color;
            Console.WriteLine(m.HealthStatus);
            Console.ResetColor();
        }

        Console.WriteLine();
        return 0;
    }

    private static async Task<int> RunCleanup(SqliteMeasurementRepository repo, int olderThanDays)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays);

        var deletedMeasurements = await repo.DeleteMeasurementsOlderThanAsync(cutoff);
        var deletedSessions = await repo.DeleteEmptySessionsAsync();

        Console.WriteLine();
        Console.WriteLine($"  Cleanup complete (older than {olderThanDays} days):");
        Console.WriteLine($"    Measurements deleted: {deletedMeasurements}");
        Console.WriteLine($"    Empty sessions removed: {deletedSessions}");
        Console.WriteLine();

        return 0;
    }

    private static (string Mode, long? SessionId, int Limit, int OlderThanDays) ParseArgs(string[] args)
    {
        var mode = "sessions";
        long? sessionId = null;
        var limit = 20;
        var olderThanDays = 30;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--session" or "-s" when i + 1 < args.Length:
                    mode = "session";
                    if (!CliParse.TryLong(args[++i], "session id", out var id))
                        return ("error", null, -1, 30);
                    if (id < 1)
                        return ("error", null, -1, 30);
                    sessionId = id;
                    break;
                case "--recent" or "-r":
                    mode = "recent";
                    break;
                case "--limit" or "-l" when i + 1 < args.Length:
                    if (!CliParse.TryInt(args[++i], "limit", out limit) || limit < 1)
                        return ("error", null, -1, 30);
                    break;
                case "--cleanup":
                    mode = "cleanup";
                    break;
                case "--older-than" when i + 1 < args.Length:
                    if (!CliParse.TryInt(args[++i], "older-than", out olderThanDays) || olderThanDays < 1)
                        return ("error", null, -1, 30);
                    break;
            }
        }

        return (mode, sessionId, limit, olderThanDays);
    }
}
