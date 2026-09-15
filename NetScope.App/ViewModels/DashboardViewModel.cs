using System.Collections.ObjectModel;
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
    public partial string LatencyDisplay { get; set; } = "— ms";

    [ObservableProperty]
    public partial string PacketLossDisplay { get; set; } = "— %";

    [ObservableProperty]
    public partial string JitterDisplay { get; set; } = "— ms";

    [ObservableProperty]
    public partial string HealthStatus { get; set; } = "Unknown";

    [ObservableProperty]
    public partial string HealthColor { get; set; } = "#888888";

    [ObservableProperty]
    public partial string MonitorTarget { get; set; } = "1.1.1.1";

    [ObservableProperty]
    public partial bool IsMonitoring { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    public ObservableCollection<MeasurementRow> RecentMeasurements { get; } = [];

    private CancellationTokenSource? _cts;

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
        }
        catch
        {
            ConnectionStatus = "Error detecting network";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StartQuickMonitorAsync()
    {
        if (IsMonitoring) return;
        IsMonitoring = true;

        _cts = new CancellationTokenSource();
        var options = new MonitorOptions
        {
            Target = MonitorTarget,
            IntervalSeconds = 3,
            ProbesPerMeasurement = 4,
            TimeoutMs = 3000
        };

        try
        {
            await foreach (var m in ServiceLocator.MonitorService.MonitorAsync(options, _cts.Token))
            {
                UpdateFromMeasurement(m);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsMonitoring = false;
        }
    }

    [RelayCommand]
    private void StopMonitor()
    {
        _cts?.Cancel();
    }

    private void UpdateFromMeasurement(NetworkMeasurement m)
    {
        LatencyDisplay = m.AvgLatencyMs.HasValue ? $"{m.AvgLatencyMs.Value:F1} ms" : "— ms";
        PacketLossDisplay = $"{m.PacketLossPercent:F0} %";
        JitterDisplay = m.JitterMs.HasValue ? $"{m.JitterMs.Value:F1} ms" : "— ms";
        HealthStatus = m.HealthStatus.ToString();
        HealthColor = m.HealthStatus switch
        {
            NetworkHealthStatus.Healthy => "#4CAF50",
            NetworkHealthStatus.Degraded => "#FF9800",
            NetworkHealthStatus.Unstable => "#F44336",
            NetworkHealthStatus.Disconnected => "#B71C1C",
            _ => "#888888"
        };
        ConnectionStatus = m.IsConnected ? "Connected" : "Disconnected";

        RecentMeasurements.Insert(0, new MeasurementRow(m));
        while (RecentMeasurements.Count > 20)
            RecentMeasurements.RemoveAt(RecentMeasurements.Count - 1);
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
            NetworkHealthStatus.Healthy => "#4CAF50",
            NetworkHealthStatus.Degraded => "#FF9800",
            NetworkHealthStatus.Unstable => "#F44336",
            NetworkHealthStatus.Disconnected => "#B71C1C",
            _ => "#888888"
        };
    }
}
