using System.Net;
using System.Net.Sockets;
using NetScope.Core.Models;
using NetScope.Infrastructure.Network;

namespace NetScope.Tests.Infrastructure;

public class PortTestServiceTests
{
    [Fact]
    public async Task TestAsync_LocalListener_IsReachable()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var acceptCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var accept = listener.AcceptTcpClientAsync(acceptCts.Token);

        var service = new PortTestService();
        var result = await service.TestAsync(new PortTestOptions
        {
            Host = "127.0.0.1",
            Port = port,
            TimeoutMs = 2000
        });

        Assert.True(result.IsReachable);
        Assert.True(result.ConnectMs.HasValue);
        listener.Stop();
        try { await accept; } catch { /* listener stopped */ }
    }

    [Fact]
    public async Task TestAsync_ClosedPort_NotReachable()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var service = new PortTestService();
        var result = await service.TestAsync(new PortTestOptions
        {
            Host = "127.0.0.1",
            Port = port,
            TimeoutMs = 500
        });

        Assert.False(result.IsReachable);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}
