using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NetScope.Core.Models;
using NetScope.Core.Networking;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Retrieves public IP information from ip-api.com (free, no key required for non-commercial use).
/// Falls back to icanhazip.com for IP-only if the primary API fails.
/// </summary>
public sealed class PublicIpService : IPublicIpService
{
    private readonly HttpClient _httpClient;

    public PublicIpService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PublicIpInfo?> GetPublicIpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetFromIpApiAsync(cancellationToken);
        }
        catch
        {
            try
            {
                return await GetFromFallbackAsync(cancellationToken);
            }
            catch
            {
                return null;
            }
        }
    }

    private async Task<PublicIpInfo?> GetFromIpApiAsync(CancellationToken ct)
    {
        var response = await _httpClient.GetFromJsonAsync<IpApiResponse>(
            "http://ip-api.com/json/?fields=query,city,regionName,country,isp,org,timezone",
            ct);

        if (response is null) return null;

        return new PublicIpInfo
        {
            IpAddress = response.Query,
            City = response.City,
            Region = response.RegionName,
            Country = response.Country,
            Isp = response.Isp,
            Organization = response.Org,
            Timezone = response.Timezone,
            RetrievedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<PublicIpInfo?> GetFromFallbackAsync(CancellationToken ct)
    {
        var ip = await _httpClient.GetStringAsync("https://icanhazip.com", ct);
        ip = ip.Trim();

        if (string.IsNullOrWhiteSpace(ip)) return null;

        return new PublicIpInfo
        {
            IpAddress = ip,
            RetrievedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class IpApiResponse
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = "";

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("regionName")]
        public string? RegionName { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("isp")]
        public string? Isp { get; set; }

        [JsonPropertyName("org")]
        public string? Org { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }
    }
}
