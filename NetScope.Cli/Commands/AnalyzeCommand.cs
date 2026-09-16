using NetScope.Cli;
using NetScope.Core.Diagnostics;
using NetScope.Core.Models;
using NetScope.Infrastructure.Persistence;

namespace NetScope.Cli.Commands;

internal static class AnalyzeCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var (sessionId, hours) = ParseArgs(args);
        if (hours < 0)
            return 1;

        await using var repo = new SqliteMeasurementRepository();
        await repo.InitializeAsync();

        IReadOnlyList<NetworkMeasurement> measurements;
        string label;

        if (sessionId.HasValue)
        {
            measurements = await repo.GetMeasurementsBySessionAsync(sessionId.Value);
            label = $"Session #{sessionId.Value}";
        }
        else
        {
            var to = DateTimeOffset.UtcNow;
            var from = to.AddHours(-hours);
            measurements = await repo.GetMeasurementsByTimeRangeAsync(from, to);
            label = $"Last {hours} hour(s)";
        }

        IDiagnosticAnalyzer analyzer = new DiagnosticAnalyzer();
        var report = analyzer.Analyze(new DiagnosticRequest { Measurements = measurements });

        PrintReport(label, report);
        return report.SampleCount == 0 ? 1 : 0;
    }

    private static void PrintReport(string label, DiagnosticReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"  ── Analysis: {label} ──");
        Console.WriteLine();

        var severityColor = report.Severity switch
        {
            DiagnosticSeverity.Info => ConsoleColor.Green,
            DiagnosticSeverity.Warning => ConsoleColor.Yellow,
            DiagnosticSeverity.Alert => ConsoleColor.Red,
            DiagnosticSeverity.Critical => ConsoleColor.DarkRed,
            _ => ConsoleColor.Gray
        };

        Console.ForegroundColor = severityColor;
        Console.WriteLine($"  {report.Headline}");
        Console.ResetColor();
        Console.WriteLine();

        if (report.SampleCount == 0)
        {
            Console.WriteLine($"  {report.Summary}");
            Console.WriteLine();
            Console.WriteLine("  Run: netscope monitor --save --count 20");
            Console.WriteLine();
            return;
        }

        Console.WriteLine($"  Target:     {report.Target}");
        Console.WriteLine($"  Samples:    {report.SampleCount}");
        Console.Write("  Severity:   ");
        Console.ForegroundColor = severityColor;
        Console.WriteLine(report.Severity);
        Console.ResetColor();

        if (report.PeriodStart.HasValue && report.PeriodEnd.HasValue)
        {
            Console.WriteLine($"  Period:     {report.PeriodStart.Value.ToLocalTime():yyyy-MM-dd HH:mm} → " +
                              $"{report.PeriodEnd.Value.ToLocalTime():yyyy-MM-dd HH:mm}");
        }

        Console.WriteLine();
        Console.WriteLine("  Findings:");

        foreach (var finding in report.Findings)
        {
            var color = finding.Severity switch
            {
                DiagnosticSeverity.Info => ConsoleColor.Green,
                DiagnosticSeverity.Warning => ConsoleColor.Yellow,
                DiagnosticSeverity.Alert => ConsoleColor.Red,
                DiagnosticSeverity.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.Gray
            };

            Console.Write("    ");
            Console.ForegroundColor = color;
            Console.Write($"{finding.Severity,-9}");
            Console.ResetColor();
            Console.Write($" {finding.Code,-18}");
            Console.WriteLine($" {finding.Detail}");
        }

        if (report.SuggestedActions.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Suggested next steps:");
            foreach (var action in report.SuggestedActions)
                Console.WriteLine($"    [{action.Label}]  {action.Reason}");
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
