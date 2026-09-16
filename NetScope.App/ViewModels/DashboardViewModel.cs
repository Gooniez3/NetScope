using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.App.Services;
using NetScope.Core.Models;

namespace NetScope.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string InterfaceName { get; set; } = "—";

    [ObservableProperty]
    public partial string LocalIp { get; set; } = "—";

    [ObservableProperty]
    public partial string Gateway { get; set; } = "—";

    [ObservableProperty]
    public partial string ConnectionStatus { get; set; } = "Checking…";

    [ObservableProperty]
    public partial string ConnectionStatusColor { get; set; } = "#6E7681";

    [ObservableProperty]
    public partial string DownRateDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial string UpRateDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    public MonitorViewModel Monitor { get; }

    private readonly Action _openMonitor;
    private CancellationTokenSource? _throughputCts;
    private long? _prevRx;
    private long? _prevTx;
    private DateTimeOffset? _prevSampleAt;
    private string? _throughputNicId;

    public DashboardViewModel(MonitorViewModel monitor, Action openMonitor)
    {
        Monitor = monitor;
        _openMonitor = openMonitor;
    }

    [RelayCommand]
    private void OpenMonitor() => _openMonitor();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                var svc = ServiceLocator.InterfaceService;
                var active = svc.GetActiveInterface();
                if (active is not null)
                {
                    InterfaceName = $"{active.Name} ({active.SpeedDisplay})";
                    LocalIp = active.IPv4Addresses.FirstOrDefault() ?? "—";
                    Gateway = active.GatewayAddresses.FirstOrDefault() ?? "—";
                }
                else
                {
                    InterfaceName = "No active interface";
                    LocalIp = "—";
                    Gateway = "—";
                }
            });

            ConnectionStatus = "Connected";
            ConnectionStatusColor = "#8B949E";
        }
        catch
        {
            ConnectionStatus = "Error";
            ConnectionStatusColor = "#F85149";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void StartThroughput()
    {
        StopThroughput();
        _throughputCts = new CancellationTokenSource();
        _ = SampleThroughputAsync(_throughputCts.Token);
    }

    public void StopThroughput()
    {
        _throughputCts?.Cancel();
        _throughputCts?.Dispose();
        _throughputCts = null;
        _prevRx = null;
        _prevTx = null;
        _prevSampleAt = null;
        _throughputNicId = null;
    }

    private async Task SampleThroughputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                SampleThroughputOnce();
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SampleThroughputOnce()
    {
        try
        {
            var active = ServiceLocator.InterfaceService.GetActiveInterface();
            Dispatcher.UIThread.Post(() => ApplyThroughputSample(active));
        }
        catch
        {
            Dispatcher.UIThread.Post(() =>
            {
                DownRateDisplay = "—";
                UpRateDisplay = "—";
            });
        }
    }

    private void ApplyThroughputSample(Core.Models.NetworkInterfaceInfo? active)
    {
        if (active is null || active.BytesReceived is null || active.BytesSent is null)
        {
            DownRateDisplay = "—";
            UpRateDisplay = "—";
            _prevRx = null;
            _prevTx = null;
            _prevSampleAt = null;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_throughputNicId != active.Id)
        {
            _throughputNicId = active.Id;
            _prevRx = active.BytesReceived;
            _prevTx = active.BytesSent;
            _prevSampleAt = now;
            DownRateDisplay = "—";
            UpRateDisplay = "—";
            return;
        }

        if (_prevRx is long prevRx && _prevTx is long prevTx && _prevSampleAt is DateTimeOffset prevAt)
        {
            var seconds = Math.Max((now - prevAt).TotalSeconds, 0.001);
            var down = Math.Max(0, active.BytesReceived.Value - prevRx) / seconds;
            var up = Math.Max(0, active.BytesSent.Value - prevTx) / seconds;
            DownRateDisplay = FormatRate(down);
            UpRateDisplay = FormatRate(up);
        }

        _prevRx = active.BytesReceived;
        _prevTx = active.BytesSent;
        _prevSampleAt = now;
    }

    private static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1_000_000)
            return $"{bytesPerSecond / 1_000_000:F1} MB/s";
        if (bytesPerSecond >= 1_000)
            return $"{bytesPerSecond / 1_000:F0} KB/s";
        return $"{bytesPerSecond:F0} B/s";
    }
}

public class MeasurementRow
{
    public string Time { get; }
    public string Target { get; }
    public string Latency { get; }
    public string Loss { get; }
    public string Jitter { get; }
    public string Status { get; }
    public string StatusColor { get; }

    public MeasurementRow(NetworkMeasurement m)
    {
        Time = m.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        Target = m.Target;
        Latency = m.AvgLatencyMs.HasValue ? $"{m.AvgLatencyMs.Value:F1} ms" : "—";
        Loss = $"{m.PacketLossPercent:F0}%";
        Jitter = m.JitterMs.HasValue ? $"{m.JitterMs.Value:F1} ms" : "—";
        Status = m.HealthStatus.ToString();
        StatusColor = m.HealthStatus switch
        {
            NetworkHealthStatus.Healthy => "#3FB950",
            NetworkHealthStatus.Degraded => "#D29922",
            NetworkHealthStatus.Unstable => "#F85149",
            NetworkHealthStatus.Disconnected => "#F85149",
            _ => "#8B949E"
        };
    }
}
