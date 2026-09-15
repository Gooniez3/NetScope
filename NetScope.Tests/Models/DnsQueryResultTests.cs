using System.Net.Sockets;
using NetScope.Core.Models;

namespace NetScope.Tests.Models;

public class DnsQueryResultTests
{
    // --- Successful result mapping ---

    [Fact]
    public void SuccessfulResult_HasCorrectProperties()
    {
        var result = new DnsQueryResult
        {
            Hostname = "example.com",
            Success = true,
            Addresses =
            [
                new DnsResolvedAddress { Address = "93.184.216.34", Family = AddressFamily.InterNetwork }
            ],
            ResolutionMs = 18.5,
            Timestamp = DateTimeOffset.UtcNow
        };

        Assert.True(result.Success);
        Assert.Equal("example.com", result.Hostname);
        Assert.Single(result.Addresses);
        Assert.Null(result.ErrorMessage);
    }

    // --- Multiple addresses ---

    [Fact]
    public void MultipleAddresses_BothFamilies()
    {
        var result = new DnsQueryResult
        {
            Hostname = "example.com",
            Success = true,
            Addresses =
            [
                new DnsResolvedAddress { Address = "93.184.216.34", Family = AddressFamily.InterNetwork },
                new DnsResolvedAddress { Address = "2606:2800:220:1:248:1893:25c8:1946", Family = AddressFamily.InterNetworkV6 }
            ],
            ResolutionMs = 22.0,
            Timestamp = DateTimeOffset.UtcNow
        };

        Assert.Equal(2, result.Addresses.Count);
        Assert.Contains(result.Addresses, a => a.Family == AddressFamily.InterNetwork);
        Assert.Contains(result.Addresses, a => a.Family == AddressFamily.InterNetworkV6);
    }

    // --- Failure result ---

    [Fact]
    public void FailedResult_HasErrorMessage()
    {
        var result = new DnsQueryResult
        {
            Hostname = "does.not.exist.invalid",
            Success = false,
            ResolutionMs = 50.0,
            Timestamp = DateTimeOffset.UtcNow,
            ErrorMessage = "No such host is known."
        };

        Assert.False(result.Success);
        Assert.Empty(result.Addresses);
        Assert.NotNull(result.ErrorMessage);
    }

    // --- Empty hostname produces failure ---

    [Fact]
    public void EmptyHostname_CanBeRepresentedAsFailure()
    {
        var result = new DnsQueryResult
        {
            Hostname = "",
            Success = false,
            ResolutionMs = 0,
            Timestamp = DateTimeOffset.UtcNow,
            ErrorMessage = "Hostname must not be empty."
        };

        Assert.False(result.Success);
        Assert.Equal("", result.Hostname);
    }

    // --- DnsResolvedAddress family label ---

    [Theory]
    [InlineData(AddressFamily.InterNetwork, "IPv4")]
    [InlineData(AddressFamily.InterNetworkV6, "IPv6")]
    public void FamilyLabel_ReturnsHumanReadable(AddressFamily family, string expected)
    {
        var addr = new DnsResolvedAddress { Address = "1.2.3.4", Family = family };
        Assert.Equal(expected, addr.FamilyLabel);
    }

    // --- Default addresses list is empty ---

    [Fact]
    public void DefaultAddressList_IsEmpty()
    {
        var result = new DnsQueryResult
        {
            Hostname = "test",
            Success = true,
            ResolutionMs = 0,
            Timestamp = DateTimeOffset.UtcNow
        };

        Assert.Empty(result.Addresses);
    }
}
