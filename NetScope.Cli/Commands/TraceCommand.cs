using NetScope.Core.Models;
using NetScope.Infrastructure.Network;

namespace NetScope.Cli.Commands;

internal static class TraceCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Usage: netscope trace <target> [--max-hops N] [--timeout MS] [--no-dns]");
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

        Console.WriteLine("TRACEROUTE");
        Console.WriteLine("────────────────────────────────────");
        Console.WriteLine($"  Target: {options.Target}");
        Console.WriteLine($"  Max hops: {options.MaxHops}, Timeout: {options.TimeoutMs} ms" +
                          (options.ResolveHostnames ? "" : ", DNS: off"));
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n  ⚠ Interrupted — showing hops collected so far…");
        };

        var traceService = new TracerouteService();
        var progress = new Progress<TracerouteHop>(PrintHop);
        var result = await traceService.TraceAsync(options, progress, cts.Token);

        Console.WriteLine();

        if (result.ErrorMessage is not null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Error: {result.ErrorMessage}");
            Console.ResetColor();
            return 2;
        }

        if (result.ResolvedAddress is not null && result.ResolvedAddress != options.Target)
            Console.WriteLine($"  Resolved: {result.ResolvedAddress}");

        Console.Write("  Destination reached: ");
        Console.ForegroundColor = result.DestinationReached ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(result.DestinationReached ? "Yes" : "No");
        Console.ResetColor();

        Console.WriteLine($"  Duration: {result.TotalDurationMs:F0} ms");

        if (result.WasCancelled)
            Console.WriteLine("  (Cancelled by user)");

        return result.DestinationReached ? 0 : 2;
    }

    private static void PrintHop(TracerouteHop hop)
    {
        var hopNum = hop.HopNumber.ToString().PadLeft(3);

        if (!hop.Responded)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {hopNum}   *  request timed out");
            Console.ResetColor();
            return;
        }

        var rttStr = hop.RoundTripTimeMs.HasValue
            ? $"{hop.RoundTripTimeMs:F0} ms".PadRight(8)
            : "?".PadRight(8);

        var addrStr = hop.Address ?? "???";
        var hostStr = hop.Hostname is not null ? $" ({hop.Hostname})" : "";

        Console.ForegroundColor = hop.Status == "Success" ? ConsoleColor.Green : ConsoleColor.White;
        Console.Write($"  {hopNum}   {addrStr}{hostStr}");
        Console.ResetColor();
        Console.WriteLine($"   {rttStr}");
    }

    private static TracerouteOptions? ParseOptions(string[] args)
    {
        var target = args[0];
        var maxHops = 30;
        var timeoutMs = 3000;
        var resolveHostnames = true;

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--no-dns")
            {
                resolveHostnames = false;
                continue;
            }

            if (!TryGetNextValue(args, ref i, arg, out var value))
                return null;

            switch (arg)
            {
                case "--max-hops" or "-m":
                    if (!int.TryParse(value, out maxHops))
                        return ParseError($"Invalid max-hops: {value}");
                    break;
                case "--timeout" or "-t":
                    if (!int.TryParse(value, out timeoutMs))
                        return ParseError($"Invalid timeout: {value}");
                    break;
                default:
                    return ParseError($"Unknown option: {arg}");
            }
        }

        return new TracerouteOptions
        {
            Target = target,
            MaxHops = maxHops,
            TimeoutMs = timeoutMs,
            ResolveHostnames = resolveHostnames
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

    private static TracerouteOptions? ParseError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
        return null;
    }
}
