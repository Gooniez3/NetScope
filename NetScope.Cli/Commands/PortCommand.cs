using NetScope.Cli;
using NetScope.Core.Models;
using NetScope.Infrastructure.Network;

namespace NetScope.Cli.Commands;

internal static class PortCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Usage: netscope port <host> [--port <n>] [--preset http|https|ssh|dns|smtp|rdp] [--timeout <ms>]");
            return 1;
        }

        var host = args[0];
        var port = 443;
        var timeout = 3000;
        var portSet = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port" or "-p":
                    if (i + 1 >= args.Length || !CliParse.TryInt(args[++i], "port", out port))
                        return 1;
                    portSet = true;
                    break;
                case "--preset":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Missing value for --preset");
                        return 1;
                    }
                    if (!PortTestOptions.TryResolvePreset(args[++i], out var presetPort))
                    {
                        Console.Error.WriteLine("Unknown preset. Use http, https, ssh, dns, smtp, or rdp.");
                        return 1;
                    }
                    if (!portSet)
                        port = presetPort;
                    break;
                case "--timeout" or "-t":
                    if (i + 1 >= args.Length || !CliParse.TryInt(args[++i], "timeout", out timeout))
                        return 1;
                    break;
                default:
                    Console.Error.WriteLine($"Unknown option: {args[i]}");
                    return 1;
            }
        }

        PortTestOptions options;
        try
        {
            options = new PortTestOptions { Host = host, Port = port, TimeoutMs = timeout };
            options.Validate();
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine("TCP PORT TEST");
        Console.WriteLine("────────────────────────");
        Console.WriteLine($"  Host: {options.Host}");
        Console.WriteLine($"  Port: {options.Port}");
        Console.WriteLine();

        var service = new PortTestService();
        var result = await service.TestAsync(options);

        if (result.IsReachable)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Status: Open");
            Console.ResetColor();
            Console.WriteLine($"  Connect: {result.ConnectMs:F1} ms");
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  Status: Closed");
        Console.ResetColor();
        if (result.ErrorMessage is not null)
            Console.WriteLine($"  Error:  {result.ErrorMessage}");
        return 2;
    }
}
