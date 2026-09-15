using NetScope.Core.Models;

namespace NetScope.Core.Networking;

/// <summary>
/// Retrieves the machine's public-facing IP address and optional metadata.
/// </summary>
public interface IPublicIpService
{
    /// <summary>
    /// Queries an external service for public IP information.
    /// </summary>
    Task<PublicIpInfo?> GetPublicIpAsync(CancellationToken cancellationToken = default);
}
