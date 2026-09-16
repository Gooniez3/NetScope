using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>Tests whether a TCP host:port accepts a connection.</summary>
public interface IPortTestService
{
    Task<PortTestResult> TestAsync(PortTestOptions options, CancellationToken cancellationToken = default);
}
