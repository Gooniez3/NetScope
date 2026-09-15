namespace NetScope.Cli.Commands;

internal static class HelpCommand
{
    public static Task<int> Run(string? error = null)
    {
        if (error is not null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(error);
            Console.ResetColor();
            Console.Error.WriteLine();
        }

        Console.WriteLine("NetScope — Network visibility. Diagnostics. Intelligence.");
        Console.WriteLine();
        Console.WriteLine("Usage: netscope <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  info                         Show network interfaces, connection, and public IP");
        Console.WriteLine("  ping <target> [options]      Ping a host and display latency statistics");
        Console.WriteLine("  dns <hostname>               Resolve a hostname and display addresses");
        Console.WriteLine("  trace <target> [options]     Traceroute to a host");
        Console.WriteLine("  help                         Show this help message");
        Console.WriteLine();
        Console.WriteLine("Ping options:");
        Console.WriteLine("  --count, -c <n>              Number of probes (1–1000, default 4)");
        Console.WriteLine("  --interval, -i <ms>          Interval between probes (100–60000, default 1000)");
        Console.WriteLine("  --timeout, -t <ms>           Timeout per probe (100–30000, default 3000)");
        Console.WriteLine("  --size, -s <bytes>           Payload size (0–65500, default 32)");
        Console.WriteLine("  --ttl <n>                    Time to live (1–255, default OS)");
        Console.WriteLine();
        Console.WriteLine("Traceroute options:");
        Console.WriteLine("  --max-hops, -m <n>           Maximum hops (1–64, default 30)");
        Console.WriteLine("  --timeout, -t <ms>           Timeout per hop (100–10000, default 3000)");
        Console.WriteLine("  --no-dns                     Skip reverse DNS on hops");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  netscope info");
        Console.WriteLine("  netscope ping 1.1.1.1");
        Console.WriteLine("  netscope ping google.com --count 10 --interval 500");
        Console.WriteLine("  netscope dns google.com");
        Console.WriteLine("  netscope trace 1.1.1.1");
        Console.WriteLine("  netscope trace google.com --max-hops 20 --no-dns");

        return Task.FromResult(error is null ? 0 : 1);
    }
}
