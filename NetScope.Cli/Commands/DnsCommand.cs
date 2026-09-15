using NetScope.Infrastructure.Network;

namespace NetScope.Cli.Commands;

internal static class DnsCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Usage: netscope dns <hostname>");
            return 1;
        }

        var hostname = args[0];

        Console.WriteLine("DNS LOOKUP");
        Console.WriteLine("────────────────────────");
        Console.WriteLine($"  Host: {hostname}");
        Console.WriteLine();

        var dnsService = new DnsService();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await dnsService.ResolveAsync(hostname, cts.Token);

        Console.WriteLine($"  Resolution: {result.ResolutionMs:F1} ms");
        Console.WriteLine();

        if (result.Success && result.Addresses.Count > 0)
        {
            Console.WriteLine("  Addresses:");
            foreach (var addr in result.Addresses)
            {
                Console.ForegroundColor = addr.FamilyLabel == "IPv4"
                    ? ConsoleColor.Cyan
                    : ConsoleColor.DarkCyan;
                Console.Write($"    {addr.Address}");
                Console.ResetColor();
                Console.WriteLine($"  ({addr.FamilyLabel})");
            }
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Status: Success");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Status: Failed");
        Console.ResetColor();
        if (result.ErrorMessage is not null)
            Console.WriteLine($"  Error:  {result.ErrorMessage}");

        return 2;
    }
}
