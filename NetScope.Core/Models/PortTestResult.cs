namespace NetScope.Core.Models;

/// <summary>Structured outcome of a TCP connect probe. Network failures are results, not exceptions.</summary>
public sealed class PortTestResult
{
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required bool IsReachable { get; init; }
    public double? ConnectMs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
