using NetScope.Core.Models;
using NetScope.Infrastructure.Network;

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║            NetScope — Network Engine             ║");
Console.WriteLine("║        Phase 1: Network Information              ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");
Console.WriteLine();

// --- Network Interfaces ---
Console.WriteLine("── Network Interfaces ──────────────────────────────");
var interfaceService = new NetworkInterfaceService();
var interfaces = interfaceService.GetAllInterfaces();

foreach (var nic in interfaces)
{
    var status = nic.IsUp ? "✓ UP" : "✗ DOWN";
    Console.WriteLine($"\n  [{status}] {nic.Name} ({nic.InterfaceType})");
    Console.WriteLine($"         Description : {nic.Description}");
    Console.WriteLine($"         Speed       : {nic.SpeedDisplay}");
    if (nic.MacAddress is not null)
        Console.WriteLine($"         MAC         : {nic.MacAddress}");
    if (nic.IPv4Addresses.Count > 0)
        Console.WriteLine($"         IPv4        : {string.Join(", ", nic.IPv4Addresses)}");
    if (nic.IPv6Addresses.Count > 0)
        Console.WriteLine($"         IPv6        : {string.Join(", ", nic.IPv6Addresses)}");
    if (nic.GatewayAddresses.Count > 0)
        Console.WriteLine($"         Gateway     : {string.Join(", ", nic.GatewayAddresses)}");
    if (nic.DnsAddresses.Count > 0)
        Console.WriteLine($"         DNS         : {string.Join(", ", nic.DnsAddresses)}");
    if (nic.IsDhcpEnabled)
        Console.WriteLine($"         DHCP Server : {nic.DhcpServer}");
}

var active = interfaceService.GetActiveInterface();
Console.WriteLine($"\n  ► Active interface: {active?.Name ?? "(none detected)"}");

// --- Connection Test ---
Console.WriteLine("\n── Connection Status ───────────────────────────────");
var connectionService = new ConnectionTestService();
var connStatus = await connectionService.TestConnectionAsync();
Console.WriteLine($"  {connStatus.Summary}");

// --- Public IP ---
Console.WriteLine("\n── Public IP ──────────────────────────────────────");
using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var publicIpService = new PublicIpService(httpClient);
var publicIp = await publicIpService.GetPublicIpAsync();

if (publicIp is not null)
{
    Console.WriteLine($"  IP       : {publicIp.IpAddress}");
    if (publicIp.Isp is not null)
        Console.WriteLine($"  ISP      : {publicIp.Isp}");
    if (publicIp.City is not null || publicIp.Country is not null)
        Console.WriteLine($"  Location : {publicIp.City}, {publicIp.Region}, {publicIp.Country}");
    if (publicIp.Timezone is not null)
        Console.WriteLine($"  Timezone : {publicIp.Timezone}");
}
else
{
    Console.WriteLine("  Could not retrieve public IP.");
}

// --- Full Snapshot ---
Console.WriteLine("\n── Full Snapshot ──────────────────────────────────");
var snapshotService = new NetworkSnapshotService(interfaceService, connectionService, publicIpService);
var snapshot = await snapshotService.CaptureSnapshotAsync();
Console.WriteLine($"  Captured at      : {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
Console.WriteLine($"  Interfaces       : {snapshot.Interfaces.Count} found ({snapshot.Interfaces.Count(i => i.IsUp)} up)");
Console.WriteLine($"  Active interface : {snapshot.ActiveInterface?.Name ?? "none"}");
Console.WriteLine($"  Connection       : {snapshot.ConnectionStatus.Summary}");
Console.WriteLine($"  Public IP        : {snapshot.PublicIp?.IpAddress ?? "unavailable"}");

Console.WriteLine("\n══════════════════════════════════════════════════════");
Console.WriteLine("Phase 1 complete. Network engine is operational.");
