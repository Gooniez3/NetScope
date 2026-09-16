using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Resolves hostnames using <see cref="Dns.GetHostEntryAsync(string, CancellationToken)"/>.
/// Cross-platform — works on Windows, Linux, and macOS via the .NET runtime.
/// </summary>
public sealed class DnsService : IDnsService
{
    public async Task<DnsQueryResult> ResolveAsync(
        string hostname,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return new DnsQueryResult
            {
                Hostname = hostname ?? "",
                Success = false,
                ResolutionMs = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ErrorMessage = "Hostname must not be empty."
            };
        }

        if (hostname.Length > HostTarget.MaxLength || hostname.Contains('\0'))
        {
            return new DnsQueryResult
            {
                Hostname = hostname,
                Success = false,
                ResolutionMs = 0,
                Timestamp = DateTimeOffset.UtcNow,
                ErrorMessage = hostname.Contains('\0')
                    ? "Hostname must not contain null characters."
                    : $"Hostname must be at most {HostTarget.MaxLength} characters."
            };
        }

        var timestamp = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();

        try
        {
            var entry = await Dns.GetHostEntryAsync(hostname, cancellationToken);
            sw.Stop();

            var addresses = entry.AddressList
                .Select(addr => new DnsResolvedAddress
                {
                    Address = addr.ToString(),
                    Family = addr.AddressFamily
                })
                .ToList();

            return new DnsQueryResult
            {
                Hostname = hostname,
                Success = true,
                Addresses = addresses,
                ResolutionMs = sw.Elapsed.TotalMilliseconds,
                Timestamp = timestamp
            };
        }
        catch (SocketException ex)
        {
            sw.Stop();
            return new DnsQueryResult
            {
                Hostname = hostname,
                Success = false,
                ResolutionMs = sw.Elapsed.TotalMilliseconds,
                Timestamp = timestamp,
                ErrorMessage = ex.Message
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new DnsQueryResult
            {
                Hostname = hostname,
                Success = false,
                ResolutionMs = sw.Elapsed.TotalMilliseconds,
                Timestamp = timestamp,
                ErrorMessage = "DNS resolution was cancelled."
            };
        }
    }
}
