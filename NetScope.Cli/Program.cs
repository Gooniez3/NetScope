using NetScope.Cli.Commands;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

return await (command switch
{
    "info" => InfoCommand.RunAsync(),
    "ping" => PingCommand.RunAsync(args[1..]),
    "dns" => DnsCommand.RunAsync(args[1..]),
    "trace" => TraceCommand.RunAsync(args[1..]),
    "scan" => ScanCommand.RunAsync(args[1..]),
    "monitor" => MonitorCommand.RunAsync(args[1..]),
    "history" => HistoryCommand.RunAsync(args[1..]),
    "stats" => StatsCommand.RunAsync(args[1..]),
    "help" or "--help" or "-h" => HelpCommand.Run(),
    _ => HelpCommand.Run($"Unknown command: {command}")
});
