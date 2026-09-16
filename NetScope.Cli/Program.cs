using NetScope.Cli.Commands;

try
{
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
        "analyze" => AnalyzeCommand.RunAsync(args[1..]),
        "help" or "--help" or "-h" => HelpCommand.Run(),
        _ => HelpCommand.Run($"Unknown command: {command}")
    });
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Error: {ex.Message}");
    Console.ResetColor();
    return 2;
}
