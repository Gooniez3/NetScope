namespace NetScope.Core.Networking;

/// <summary>
/// Shared validation for hostnames and IP literals passed to ping, DNS, traceroute, monitor, and TCP port tests.
/// </summary>
public static class HostTarget
{
    /// <summary>RFC 1035 maximum length of a DNS name.</summary>
    public const int MaxLength = 253;

    /// <summary>
    /// Validates a host or IP string. Throws <see cref="ArgumentException"/> when empty,
    /// longer than <see cref="MaxLength"/>, or containing a null character.
    /// </summary>
    public static void Validate(string? value, string paramName = "Target")
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Target must not be empty.", paramName);

        if (value.Length > MaxLength)
            throw new ArgumentException(
                $"Target must be at most {MaxLength} characters.", paramName);

        if (value.Contains('\0'))
            throw new ArgumentException("Target must not contain null characters.", paramName);
    }
}
