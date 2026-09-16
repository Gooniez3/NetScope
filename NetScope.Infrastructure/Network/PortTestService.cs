using System.Diagnostics;
using System.Net.Sockets;
using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// TCP connect probe using <see cref="TcpClient"/>. Timeouts and refused ports are structured results.
/// </summary>
public sealed class PortTestService : IPortTestService
{
    public async Task<PortTestResult> TestAsync(PortTestOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.TimeoutMs);

        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(options.Host, options.Port, timeoutCts.Token);
            sw.Stop();

            return new PortTestResult
            {
                Host = options.Host,
                Port = options.Port,
                IsReachable = true,
                ConnectMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return Unreachable(options, sw, "Timed out");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException ex)
        {
            sw.Stop();
            return Unreachable(options, sw, ex.Message);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return Unreachable(options, sw, ex.Message);
        }
    }

    private static PortTestResult Unreachable(PortTestOptions options, Stopwatch sw, string error) => new()
    {
        Host = options.Host,
        Port = options.Port,
        IsReachable = false,
        ConnectMs = sw.ElapsedMilliseconds > 0 ? sw.Elapsed.TotalMilliseconds : null,
        ErrorMessage = error
    };
}
