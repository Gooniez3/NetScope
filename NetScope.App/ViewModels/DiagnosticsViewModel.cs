using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.App.Services;
using NetScope.Core.Models;

namespace NetScope.App.ViewModels;

public partial class DiagnosticsViewModel : ViewModelBase
{
    // --- Common ---
    [ObservableProperty]
    public partial string SelectedTool { get; set; } = "Ping";

    // --- Ping ---
    [ObservableProperty]
    public partial string PingTarget { get; set; } = "1.1.1.1";

    [ObservableProperty]
    public partial int PingCount { get; set; } = 4;

    [ObservableProperty]
    public partial bool IsPinging { get; set; }

    [ObservableProperty]
    public partial string PingStatus { get; set; } = "Ready";

    [ObservableProperty]
    public partial string PingSummary { get; set; } = "";

    public ObservableCollection<PingResultRow> PingResults { get; } = [];

    // --- DNS ---
    [ObservableProperty]
    public partial string DnsHostname { get; set; } = "google.com";

    [ObservableProperty]
    public partial bool IsResolving { get; set; }

    [ObservableProperty]
    public partial string DnsStatus { get; set; } = "Ready";

    public ObservableCollection<DnsResultRow> DnsResults { get; } = [];

    // --- Traceroute ---
    [ObservableProperty]
    public partial string TraceTarget { get; set; } = "1.1.1.1";

    [ObservableProperty]
    public partial bool IsTracing { get; set; }

    [ObservableProperty]
    public partial string TraceStatus { get; set; } = "Ready";

    public ObservableCollection<TraceHopRow> TraceHops { get; } = [];

    // --- LAN Scan ---
    [ObservableProperty]
    public partial string ScanSubnet { get; set; } = "";

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string ScanStatus { get; set; } = "Ready";

    public ObservableCollection<DeviceRow> ScannedDevices { get; } = [];

    private CancellationTokenSource? _cts;

    // --- Ping Commands ---

    [RelayCommand]
    private async Task RunPingAsync()
    {
        if (IsPinging) return;
        IsPinging = true;
        PingResults.Clear();
        PingSummary = "";
        PingStatus = "Pinging…";
        _cts = new CancellationTokenSource();

        try
        {
            var options = new PingOptions
            {
                Target = PingTarget,
                Count = PingCount,
                IntervalMs = 500,
                TimeoutMs = 3000
            };

            var progress = new Progress<PingResult>(r =>
            {
                PingResults.Add(new PingResultRow(r));
            });

            var (_, stats) = await ServiceLocator.PingService.PingAsync(options, progress, _cts.Token);
            PingSummary = $"Sent: {stats.Sent}  Recv: {stats.Received}  Loss: {stats.PacketLossPercent:F0}%  " +
                          $"Avg: {stats.AvgRoundTripMs?.ToString("F1") ?? "—"} ms  " +
                          $"Jitter: {stats.JitterMs?.ToString("F1") ?? "—"} ms";
            PingStatus = "Complete";
        }
        catch (OperationCanceledException)
        {
            PingStatus = "Cancelled";
        }
        catch (Exception ex)
        {
            PingStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsPinging = false;
        }
    }

    // --- DNS Commands ---

    [RelayCommand]
    private async Task RunDnsAsync()
    {
        if (IsResolving) return;
        IsResolving = true;
        DnsResults.Clear();
        DnsStatus = "Resolving…";

        try
        {
            var result = await ServiceLocator.DnsService.ResolveAsync(DnsHostname);
            if (result.Success)
            {
                foreach (var addr in result.Addresses)
                    DnsResults.Add(new DnsResultRow(addr.Address, addr.FamilyLabel));
                DnsStatus = $"Resolved in {result.ResolutionMs:F1} ms — {result.Addresses.Count} address(es)";
            }
            else
            {
                DnsStatus = $"Failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            DnsStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsResolving = false;
        }
    }

    // --- Traceroute Commands ---

    [RelayCommand]
    private async Task RunTraceAsync()
    {
        if (IsTracing) return;
        IsTracing = true;
        TraceHops.Clear();
        TraceStatus = "Tracing…";
        _cts = new CancellationTokenSource();

        try
        {
            var options = new TracerouteOptions
            {
                Target = TraceTarget,
                MaxHops = 30,
                TimeoutMs = 3000
            };

            var progress = new Progress<TracerouteHop>(hop =>
            {
                TraceHops.Add(new TraceHopRow(hop));
            });

            var result = await ServiceLocator.TracerouteService.TraceAsync(options, progress, _cts.Token);
            TraceStatus = result.DestinationReached
                ? $"Reached {result.Target} in {result.Hops.Count} hop(s) — {result.TotalDurationMs:F0} ms"
                : $"Did not reach destination ({result.Hops.Count} hops)";
        }
        catch (OperationCanceledException)
        {
            TraceStatus = "Cancelled";
        }
        catch (Exception ex)
        {
            TraceStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsTracing = false;
        }
    }

    // --- LAN Scan Commands ---

    [RelayCommand]
    private async Task RunScanAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        ScannedDevices.Clear();
        ScanStatus = "Scanning…";
        _cts = new CancellationTokenSource();

        try
        {
            var options = new ScanOptions
            {
                Subnet = string.IsNullOrWhiteSpace(ScanSubnet) ? null : ScanSubnet,
                TimeoutMs = 500,
                Concurrency = 32
            };

            var progress = new Progress<DiscoveredDevice>(d =>
            {
                ScannedDevices.Add(new DeviceRow(d));
            });

            var result = await ServiceLocator.ScannerService.ScanAsync(options, progress, _cts.Token);
            ScanStatus = $"Found {result.Devices.Count} device(s) in {result.ScannedRange} — {result.Duration.TotalSeconds:F1}s";
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Cancelled";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void CancelOperation()
    {
        _cts?.Cancel();
    }
}

public class PingResultRow
{
    public int Seq { get; }
    public string Status { get; }
    public string Rtt { get; }
    public string StatusColor { get; }

    public PingResultRow(PingResult r)
    {
        Seq = r.SequenceNumber;
        Status = r.Success ? "Reply" : r.Status;
        Rtt = r.RoundTripTimeMs.HasValue ? $"{r.RoundTripTimeMs.Value:F1} ms" : "—";
        StatusColor = r.Success ? "#4CAF50" : "#F44336";
    }
}

public class DnsResultRow
{
    public string Address { get; }
    public string Family { get; }

    public DnsResultRow(string address, string family)
    {
        Address = address;
        Family = family;
    }
}

public class TraceHopRow
{
    public int Hop { get; }
    public string Address { get; }
    public string Hostname { get; }
    public string Rtt { get; }
    public string StatusColor { get; }

    public TraceHopRow(TracerouteHop h)
    {
        Hop = h.HopNumber;
        Address = h.Address ?? "*";
        Hostname = h.Hostname ?? "";
        Rtt = h.Responded && h.RoundTripTimeMs.HasValue ? $"{h.RoundTripTimeMs.Value:F1} ms" : "*";
        StatusColor = h.Responded ? "#4CAF50" : "#888888";
    }
}

public class DeviceRow
{
    public string Ip { get; }
    public string Hostname { get; }
    public string Mac { get; }
    public string ResponseTime { get; }
    public string Status { get; }

    public DeviceRow(DiscoveredDevice d)
    {
        Ip = d.IpAddress;
        Hostname = d.Hostname ?? "—";
        Mac = d.MacAddress ?? "—";
        ResponseTime = $"{d.ResponseTimeMs:F1} ms";
        Status = d.Status;
    }
}
