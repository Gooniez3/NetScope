using NetScope.Core.Models;
using NetScope.Infrastructure.Network;

namespace NetScope.Cli.Commands;

internal static class PingCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Usage: netscope ping <target> [--count N] [--interval MS] [--timeout MS] [--size BYTES] [--ttl N]");
            return 1;
        }

        var options = ParseOptions(args);
        if (options is null)
            return 1;

        try
        {
            options.Validate();
        }
        catch (ArgumentException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Invalid option: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"PING {options.Target} — {options.Count} probes, " +
                          $"{options.PayloadSize} bytes, timeout {options.TimeoutMs} ms" +
                          (options.Ttl.HasValue ? $", TTL {options.Ttl}" : "") +
                          $", interval {options.IntervalMs} ms");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n  ⚠ Interrupted — computing statistics for completed probes…");
        };

        var pingService = new PingService();
        var progress = new Progress<PingResult>(PrintProbe);

        var (results, stats) = await pingService.PingAsync(options, progress, cts.Token);

        Console.WriteLine();
        PrintStatistics(options.Target, stats);

        return stats.PacketLossPercent >= 100.0 ? 2 : 0;
    }

    private static void PrintProbe(PingResult result)
    {
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  ✓ ");
            Console.ResetColor();
            Console.Write($"seq={result.SequenceNumber,-4} ");
            Console.Write($"time={result.RoundTripTimeMs:F1} ms");
            if (result.ReplyTtl.HasValue)
                Console.Write($"  ttl={result.ReplyTtl}");
            if (result.ReplyBufferSize.HasValue)
                Console.Write($"  bytes={result.ReplyBufferSize}");
            Console.WriteLine();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  ✗ ");
            Console.ResetColor();
            Console.WriteLine($"seq={result.SequenceNumber,-4} {result.Status}: {result.ErrorMessage}");
        }
    }

    private static void PrintStatistics(string target, PingStatistics stats)
    {
        Console.WriteLine($"── Ping statistics for {target} ────────────────────");
        Console.WriteLine($"  Sent       : {stats.Sent}");
        Console.WriteLine($"  Received   : {stats.Received}");

        Console.ForegroundColor = stats.PacketLossPercent switch
        {
            0 => ConsoleColor.Green,
            < 10 => ConsoleColor.Yellow,
            _ => ConsoleColor.Red
        };
        Console.Write($"  Packet loss: {stats.PacketLossPercent:F1}%");
        Console.ResetColor();
        Console.WriteLine();

        if (stats.MinRoundTripMs.HasValue)
        {
            Console.WriteLine();
            Console.WriteLine($"  Min RTT    : {stats.MinRoundTripMs:F2} ms");
            Console.WriteLine($"  Max RTT    : {stats.MaxRoundTripMs:F2} ms");
            Console.WriteLine($"  Avg RTT    : {stats.AvgRoundTripMs:F2} ms");
        }

        if (stats.JitterMs.HasValue)
        {
            Console.ForegroundColor = stats.JitterMs switch
            {
                < 2 => ConsoleColor.Green,
                < 10 => ConsoleColor.Yellow,
                _ => ConsoleColor.Red
            };
            Console.Write($"  Jitter     : {stats.JitterMs:F2} ms");
            Console.ResetColor();
            Console.WriteLine();
        }
    }

    private static PingOptions? ParseOptions(string[] args)
    {
        var target = args[0];
        var count = 4;
        var intervalMs = 1000;
        var timeoutMs = 3000;
        var payloadSize = 32;
        int? ttl = null;

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];

            if (!TryGetNextValue(args, ref i, arg, out var value))
                return null;

            switch (arg)
            {
                case "--count" or "-c":
                    if (!int.TryParse(value, out count))
                        return ParseError($"Invalid count: {value}");
                    break;
                case "--interval" or "-i":
                    if (!int.TryParse(value, out intervalMs))
                        return ParseError($"Invalid interval: {value}");
                    break;
                case "--timeout" or "-t":
                    if (!int.TryParse(value, out timeoutMs))
                        return ParseError($"Invalid timeout: {value}");
                    break;
                case "--size" or "-s":
                    if (!int.TryParse(value, out payloadSize))
                        return ParseError($"Invalid size: {value}");
                    break;
                case "--ttl":
                    if (!int.TryParse(value, out var ttlValue))
                        return ParseError($"Invalid TTL: {value}");
                    ttl = ttlValue;
                    break;
                default:
                    return ParseError($"Unknown option: {arg}");
            }
        }

        return new PingOptions
        {
            Target = target,
            Count = count,
            IntervalMs = intervalMs,
            TimeoutMs = timeoutMs,
            PayloadSize = payloadSize,
            Ttl = ttl
        };
    }

    private static bool TryGetNextValue(string[] args, ref int index, string flag, out string value)
    {
        if (index + 1 < args.Length)
        {
            value = args[++index];
            return true;
        }

        Console.Error.WriteLine($"Option {flag} requires a value.");
        value = "";
        return false;
    }

    private static PingOptions? ParseError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
        return null;
    }
}
