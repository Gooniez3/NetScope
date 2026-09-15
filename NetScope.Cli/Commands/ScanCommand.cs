using NetScope.Core.Models;
using NetScope.Infrastructure.Network;

namespace NetScope.Cli.Commands;

internal static class ScanCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
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

        Console.WriteLine("LAN SCAN");
        Console.WriteLine("────────────────────────────────────────");

        var subnetLabel = options.Subnet ?? "(auto-detect)";
        Console.WriteLine($"  Subnet     : {subnetLabel}");
        Console.WriteLine($"  Timeout    : {options.TimeoutMs} ms");
        Console.WriteLine($"  Concurrency: {options.Concurrency}");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n  ⚠ Interrupted — showing devices discovered so far…");
        };

        var interfaceService = new NetworkInterfaceService();
        var scanner = new NetworkScannerService(interfaceService);

        var deviceCount = 0;
        var consoleLock = new object();
        var progress = new Progress<DiscoveredDevice>(device =>
        {
            lock (consoleLock)
            {
                if (Interlocked.Increment(ref deviceCount) == 1)
                {
                    PrintTableHeader();
                }
                PrintDevice(device);
            }
        });

        NetworkScanResult result;
        try
        {
            result = await scanner.ScanAsync(options, progress, cts.Token);
        }
        catch (InvalidOperationException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"  Error: {ex.Message}");
            Console.ResetColor();
            return 2;
        }
        catch (FormatException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"  Invalid subnet: {ex.Message}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("────────────────────────────────────────");
        Console.WriteLine($"  Network   : {result.ScannedRange}");
        Console.WriteLine($"  Scanned   : {result.AddressesScanned} addresses");

        Console.ForegroundColor = result.DevicesDiscovered > 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.Write($"  Discovered: {result.DevicesDiscovered} devices");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine($"  Duration  : {result.Duration.TotalSeconds:F1} s");

        if (result.WasCancelled)
            Console.WriteLine("  (Cancelled by user)");

        return 0;
    }

    private static void PrintTableHeader()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  {"IP",-17} {"Hostname",-26} {"MAC",-19} {"Latency",8}");
        Console.WriteLine($"  {"─".PadRight(17, '─')} {"─".PadRight(26, '─')} {"─".PadRight(19, '─')} {"─".PadRight(8, '─')}");
        Console.ResetColor();
    }

    private static void PrintDevice(DiscoveredDevice device)
    {
        var hostname = device.Hostname ?? "–";
        var mac = device.MacAddress ?? "–";
        var latency = $"{device.ResponseTimeMs:F0} ms";

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {device.IpAddress,-17}");
        Console.ResetColor();
        Console.Write($" {Truncate(hostname, 26),-26}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($" {mac,-19}");
        Console.ResetColor();
        Console.WriteLine($" {latency,8}");
    }

    private static string Truncate(string value, int maxLen)
        => value.Length <= maxLen ? value : string.Concat(value.AsSpan(0, maxLen - 1), "…");

    private static ScanOptions? ParseOptions(string[] args)
    {
        string? subnet = null;
        var timeoutMs = 500;
        var concurrency = 32;
        var resolveHostnames = true;
        var resolveMac = true;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--no-dns")
            {
                resolveHostnames = false;
                continue;
            }
            if (arg == "--no-mac")
            {
                resolveMac = false;
                continue;
            }

            if (!TryGetNextValue(args, ref i, arg, out var value))
                return null;

            switch (arg)
            {
                case "--subnet" or "-s":
                    subnet = value;
                    break;
                case "--timeout" or "-t":
                    if (!int.TryParse(value, out timeoutMs))
                        return ParseError($"Invalid timeout: {value}");
                    break;
                case "--concurrency" or "-c":
                    if (!int.TryParse(value, out concurrency))
                        return ParseError($"Invalid concurrency: {value}");
                    break;
                default:
                    return ParseError($"Unknown option: {arg}");
            }
        }

        return new ScanOptions
        {
            Subnet = subnet,
            TimeoutMs = timeoutMs,
            Concurrency = concurrency,
            ResolveHostnames = resolveHostnames,
            ResolveMacAddresses = resolveMac
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

    private static ScanOptions? ParseError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
        return null;
    }
}
