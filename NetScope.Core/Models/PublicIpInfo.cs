namespace NetScope.Core.Models;

/// <summary>
/// Public IP address and optional geolocation metadata.
/// </summary>
public sealed class PublicIpInfo
{
    public required string IpAddress { get; init; }
    public required DateTimeOffset RetrievedAt { get; init; }

    public string? City { get; init; }
    public string? Region { get; init; }
    public string? Country { get; init; }
    public string? Isp { get; init; }
    public string? Organization { get; init; }
    public string? Timezone { get; init; }
}
