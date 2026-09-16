using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Discovers devices on the local network using a concurrent ICMP ping sweep.
/// Optionally enriches results with MAC addresses from the OS ARP table
/// and hostnames from reverse DNS.
/// </summary>
public sealed class NetworkScannerService : INetworkScannerService
{
    private readonly INetworkInterfaceService _interfaceService;

    public NetworkScannerService(INetworkInterfaceService interfaceService)
    {
        _interfaceService = interfaceService;
    }

    public async Task<NetworkScanResult> ScanAsync(
        ScanOptions options,
        IProgress<DiscoveredDevice>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options.Validate();

        var startedAt = DateTimeOffset.UtcNow;
        var (networkAddress, prefixLength) = ResolveSubnet(options);
        var cidr = SubnetHelper.ToCidrString(networkAddress, prefixLength);
        var hosts = SubnetHelper.GetHostAddresses(networkAddress, prefixLength);

        var discovered = new ConcurrentBag<DiscoveredDevice>();
        using var semaphore = new SemaphoreSlim(options.Concurrency, options.Concurrency);

        // Pre-fetch ARP table so we can enrich results without per-device lookups.
        var arpTable = options.ResolveMacAddresses
            ? await ArpTableReader.ReadArpTableAsync(cancellationToken)
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

        var tasks = new List<Task>(Math.Min(hosts.Count, 1024));

        try
        {
            foreach (var host in hosts)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                tasks.Add(ProbeHostAsync(
                    host, options, arpTable, semaphore, discovered, progress, cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Partial results — wait for in-flight probes below.
        }

        // Wait for all in-flight probes to complete (or cancel).
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected — partial results are fine.
        }

        var sorted = discovered
            .OrderBy(d => ToSortableIp(d.IpAddress))
            .ToList();

        return new NetworkScanResult
        {
            ScannedRange = cidr,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            AddressesScanned = hosts.Count,
            Devices = sorted,
            WasCancelled = cancellationToken.IsCancellationRequested
        };
    }

    private static async Task ProbeHostAsync(
        IPAddress host,
        ScanOptions options,
        IReadOnlyDictionary<string, string> arpTable,
        SemaphoreSlim semaphore,
        ConcurrentBag<DiscoveredDevice> discovered,
        IProgress<DiscoveredDevice>? progress,
        CancellationToken ct)
    {
        try
        {
            if (ct.IsCancellationRequested) return;

            var ip = host.ToString();

            using var ping = new Ping();
            var sw = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(host, options.TimeoutMs).WaitAsync(ct);
            sw.Stop();

            if (reply.Status != IPStatus.Success)
                return;

            var rtt = reply.RoundtripTime > 0
                ? (double)reply.RoundtripTime
                : sw.Elapsed.TotalMilliseconds;

            string? hostname = null;
            if (options.ResolveHostnames)
            {
                hostname = await TryReverseDnsAsync(ip, ct);
            }

            arpTable.TryGetValue(ip, out var mac);

            var device = new DiscoveredDevice
            {
                IpAddress = ip,
                Hostname = hostname,
                MacAddress = mac,
                ResponseTimeMs = rtt,
                DiscoveryMethod = "ICMP",
                Status = "Online"
            };

            discovered.Add(device);
            progress?.Report(device);
        }
        catch (OperationCanceledException)
        {
            // Cancelled — don't add.
        }
        catch
        {
            // Individual probe failures are silently ignored.
        }
        finally
        {
            semaphore.Release();
        }
    }

    private (IPAddress Network, int PrefixLength) ResolveSubnet(ScanOptions options)
    {
        if (options.Subnet is not null)
        {
            return SubnetHelper.ParseCidr(options.Subnet);
        }

        // Auto-detect from active interface.
        var active = _interfaceService.GetActiveInterface();
        if (active is null || active.IPv4Addresses.Count == 0)
            throw new InvalidOperationException(
                "No active network interface found. Specify a subnet explicitly with --subnet.");

        var ipStr = active.IPv4Addresses[0];
        var info = SubnetHelper.InferSubnetFromInterface(ipStr, active.IPv4UnicastDetails);
        if (info is null)
            throw new InvalidOperationException(
                $"Could not determine subnet for {ipStr}. Specify a subnet explicitly with --subnet.");

        var (network, prefix) = info.Value;
        if (prefix < ScanOptions.MinPrefixLength)
        {
            // Wide DHCP prefixes (e.g. /8) must not trigger a multi-million-host sweep.
            if (!IPAddress.TryParse(ipStr, out var ip))
                throw new InvalidOperationException(
                    $"Could not determine subnet for {ipStr}. Specify a subnet explicitly with --subnet.");
            prefix = 24;
            network = SubnetHelper.GetNetworkAddress(ip, prefix);
        }

        return (network, prefix);
    }

    private static async Task<string?> TryReverseDnsAsync(string ip, CancellationToken ct)
    {
        try
        {
            var entry = await System.Net.Dns.GetHostEntryAsync(ip, ct);
            return entry.HostName != ip ? entry.HostName : null;
        }
        catch
        {
            return null;
        }
    }

    private static uint ToSortableIp(string ip)
    {
        var bytes = IPAddress.Parse(ip).GetAddressBytes();
        return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
    }
}
