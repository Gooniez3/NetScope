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
        Console.WriteLine("  scan [options]               Scan the local network for devices");
        Console.WriteLine("  monitor [options]            Continuous network health monitoring");
        Console.WriteLine("  history [options]            View saved monitoring sessions and measurements");
        Console.WriteLine("  stats [options]              Show aggregate statistics from saved data");
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
        Console.WriteLine("Scan options:");
        Console.WriteLine("  --subnet, -s <cidr>          Subnet to scan (default: auto-detect)");
        Console.WriteLine("  --timeout, -t <ms>           Timeout per probe (100–10000, default 500)");
        Console.WriteLine("  --concurrency, -c <n>        Max concurrent probes (1–256, default 32)");
        Console.WriteLine("  --no-dns                     Skip reverse DNS on devices");
        Console.WriteLine("  --no-mac                     Skip MAC address lookup");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  netscope info");
        Console.WriteLine("  netscope ping 1.1.1.1");
        Console.WriteLine("  netscope ping google.com --count 10 --interval 500");
        Console.WriteLine("  netscope dns google.com");
        Console.WriteLine("  netscope trace 1.1.1.1");
        Console.WriteLine("  netscope trace google.com --max-hops 20 --no-dns");
        Console.WriteLine("  netscope scan");
        Console.WriteLine("  netscope scan --subnet 192.168.1.0/24 --timeout 500");
        Console.WriteLine("  netscope scan --concurrency 64 --no-dns --no-mac");
        Console.WriteLine();
        Console.WriteLine("Monitor options:");
        Console.WriteLine("  --target <host>              Target to monitor (default 1.1.1.1)");
        Console.WriteLine("  --interval, -i <seconds>     Seconds between cycles (1–300, default 5)");
        Console.WriteLine("  --timeout, -t <ms>           Timeout per probe (100–30000, default 3000)");
        Console.WriteLine("  --probes, -p <n>             Probes per cycle (1–20, default 4)");
        Console.WriteLine("  --count, -c <n>              Number of cycles (default: unlimited)");
        Console.WriteLine("  --dns                        Include DNS resolution timing");
        Console.WriteLine("  --gateway [addr]             Include gateway latency (auto-detect or specify)");
        Console.WriteLine();
        Console.WriteLine("Monitor examples:");
        Console.WriteLine("  netscope monitor --target 1.1.1.1");
        Console.WriteLine("  netscope monitor --interval 5 --count 20");
        Console.WriteLine("  netscope monitor --target google.com --dns --count 10");
        Console.WriteLine("  netscope monitor --save --count 5          Save measurements to database");
        Console.WriteLine();
        Console.WriteLine("History options:");
        Console.WriteLine("  --session, -s <id>           View measurements for a specific session");
        Console.WriteLine("  --recent, -r                 Show recent measurements across all sessions");
        Console.WriteLine("  --limit, -l <n>              Number of items to show (default 20)");
        Console.WriteLine("  --cleanup                    Delete old data");
        Console.WriteLine("  --older-than <days>          Retention cutoff for cleanup (default 30)");
        Console.WriteLine();
        Console.WriteLine("Stats options:");
        Console.WriteLine("  --session, -s <id>           Statistics for a specific session");
        Console.WriteLine("  --hours, -h <n>              Time range in hours (default 24)");
        Console.WriteLine();
        Console.WriteLine("History/Stats examples:");
        Console.WriteLine("  netscope history                           List recent sessions");
        Console.WriteLine("  netscope history --session 1               View session #1 measurements");
        Console.WriteLine("  netscope history --recent --limit 10       Recent measurements");
        Console.WriteLine("  netscope history --cleanup --older-than 7  Delete data older than 7 days");
        Console.WriteLine("  netscope stats                             Stats for last 24 hours");
        Console.WriteLine("  netscope stats --session 1                 Stats for session #1");
        Console.WriteLine("  netscope stats --hours 48                  Stats for last 48 hours");

        return Task.FromResult(error is null ? 0 : 1);
    }
}
