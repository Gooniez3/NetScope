using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NetScope.Infrastructure.Network;

/// <summary>
/// Reads the OS ARP table to obtain MAC addresses for known IP addresses.
/// Isolated in Infrastructure because this is inherently platform-specific.
/// </summary>
/// <remarks>
/// <b>Platform behavior:</b>
/// <list type="bullet">
///   <item>Windows: parses output of <c>arp -a</c>.</item>
///   <item>Linux: parses <c>/proc/net/arp</c> (no subprocess needed).</item>
///   <item>macOS: parses output of <c>arp -an</c>.</item>
/// </list>
/// Returns an empty dictionary on unsupported platforms or on failure.
/// Never throws — failure to read the ARP table is not a scan-breaking error.
/// </remarks>
internal static partial class ArpTableReader
{
    /// <summary>
    /// Returns a dictionary mapping IP address strings to MAC address strings.
    /// MAC addresses are formatted as XX:XX:XX:XX:XX:XX.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ReadArpTableAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return await ReadWindowsArpAsync(cancellationToken);

            if (OperatingSystem.IsLinux())
                return await ReadLinuxArpAsync(cancellationToken);

            if (OperatingSystem.IsMacOS())
                return await ReadMacOsArpAsync(cancellationToken);

            return new Dictionary<string, string>();
        }
        catch (OperationCanceledException)
        {
            return new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadWindowsArpAsync(
        CancellationToken cancellationToken)
    {
        var output = await RunProcessAsync("arp", "-a", cancellationToken);
        var result = new Dictionary<string, string>();

        foreach (var line in output.Split('\n'))
        {
            var match = WindowsArpRegex().Match(line);
            if (match.Success)
            {
                var ip = match.Groups[1].Value.Trim();
                var mac = NormalizeMac(match.Groups[2].Value.Trim());
                if (mac is not null && mac != "FF:FF:FF:FF:FF:FF")
                    result[ip] = mac;
            }
        }

        return result;
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadLinuxArpAsync(
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>();

        try
        {
            var content = await File.ReadAllTextAsync("/proc/net/arp", cancellationToken);
            foreach (var line in content.Split('\n').Skip(1)) // skip header
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    var ip = parts[0];
                    var mac = NormalizeMac(parts[3]);
                    if (mac is not null && mac != "00:00:00:00:00:00")
                        result[ip] = mac;
                }
            }
        }
        catch
        {
            // /proc/net/arp may not exist in some environments
        }

        return result;
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadMacOsArpAsync(
        CancellationToken cancellationToken)
    {
        var output = await RunProcessAsync("arp", "-an", cancellationToken);
        var result = new Dictionary<string, string>();

        foreach (var line in output.Split('\n'))
        {
            var match = MacOsArpRegex().Match(line);
            if (match.Success)
            {
                var ip = match.Groups[1].Value;
                var mac = NormalizeMac(match.Groups[2].Value);
                if (mac is not null && mac != "FF:FF:FF:FF:FF:FF")
                    result[ip] = mac;
            }
        }

        return result;
    }

    private static string? NormalizeMac(string raw)
    {
        // Accept both XX-XX-XX-XX-XX-XX and XX:XX:XX:XX:XX:XX and x:x:x:x:x:x
        var cleaned = raw.Replace('-', ':').ToUpperInvariant();
        var parts = cleaned.Split(':');
        if (parts.Length != 6) return null;

        // Pad single-digit parts (macOS sometimes outputs e.g. "0:1b:44:...")
        return string.Join(":", parts.Select(p => p.PadLeft(2, '0')));
    }

    private static async Task<string> RunProcessAsync(
        string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return output;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort stop if arp hangs.
            }

            cancellationToken.ThrowIfCancellationRequested();
            return "";
        }
    }

    // Windows: "  192.168.1.1          aa-bb-cc-dd-ee-ff     dynamic"
    [GeneratedRegex(@"^\s+(\d+\.\d+\.\d+\.\d+)\s+([0-9a-fA-F-]{17})", RegexOptions.Multiline)]
    private static partial Regex WindowsArpRegex();

    // macOS: "? (192.168.1.1) at aa:bb:cc:dd:ee:ff on en0 ..."
    [GeneratedRegex(@"\((\d+\.\d+\.\d+\.\d+)\)\s+at\s+([0-9a-fA-F:]+)", RegexOptions.Multiline)]
    private static partial Regex MacOsArpRegex();
}
