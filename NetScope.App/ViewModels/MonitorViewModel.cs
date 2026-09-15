using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.App.Services;
using NetScope.Core.Models;

namespace NetScope.App.ViewModels;

public partial class MonitorViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Target { get; set; } = "1.1.1.1";

    [ObservableProperty]
    public partial int IntervalSeconds { get; set; } = 5;

    [ObservableProperty]
    public partial int ProbesPerCycle { get; set; } = 4;

    [ObservableProperty]
    public partial int TimeoutMs { get; set; } = 3000;

    [ObservableProperty]
    public partial bool SaveToDatabase { get; set; } = true;

    [ObservableProperty]
    public partial bool IsMonitoring { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready";

    [ObservableProperty]
    public partial string HealthStatus { get; set; } = "—";

    [ObservableProperty]
    public partial string HealthColor { get; set; } = "#888888";

    [ObservableProperty]
    public partial string LatencyDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial string LossDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial string JitterDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial int CycleCount { get; set; }

    public ObservableCollection<MeasurementRow> Measurements { get; } = [];
    public ObservableCollection<LatencyPoint> LatencyHistory { get; } = [];

    private CancellationTokenSource? _cts;
    private long _sessionId;

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        CycleCount = 0;
        Measurements.Clear();
        LatencyHistory.Clear();
        StatusText = "Monitoring…";
        _cts = new CancellationTokenSource();

        var options = new MonitorOptions
        {
            Target = Target,
            IntervalSeconds = IntervalSeconds,
            ProbesPerMeasurement = ProbesPerCycle,
            TimeoutMs = TimeoutMs
        };

        if (SaveToDatabase)
        {
            try
            {
                var session = await ServiceLocator.Repository.CreateSessionAsync(
                    new MonitoringSession
                    {
                        Id = 0,
                        StartedAt = DateTimeOffset.UtcNow,
                        Target = Target,
                        IntervalSeconds = IntervalSeconds,
                        ProbesPerMeasurement = ProbesPerCycle,
                        TimeoutMs = TimeoutMs
                    });
                _sessionId = session.Id;
            }
            catch
            {
                SaveToDatabase = false;
            }
        }

        try
        {
            await foreach (var m in ServiceLocator.MonitorService.MonitorAsync(options, _cts.Token))
            {
                CycleCount++;
                UpdateFromMeasurement(m);

                if (SaveToDatabase && _sessionId > 0)
                {
                    try
                    {
                        await ServiceLocator.Repository.SaveMeasurementAsync(_sessionId, m);
                    }
                    catch { }
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (SaveToDatabase && _sessionId > 0)
            {
                try
                {
                    await ServiceLocator.Repository.CompleteSessionAsync(
                        _sessionId, DateTimeOffset.UtcNow, CycleCount, !_cts.IsCancellationRequested);
                }
                catch { }
            }

            IsMonitoring = false;
            StatusText = $"Stopped — {CycleCount} cycle(s)";
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _cts?.Cancel();
    }

    private void UpdateFromMeasurement(NetworkMeasurement m)
    {
        HealthStatus = m.HealthStatus.ToString();
        HealthColor = m.HealthStatus switch
        {
            NetworkHealthStatus.Healthy => "#4CAF50",
            NetworkHealthStatus.Degraded => "#FF9800",
            NetworkHealthStatus.Unstable => "#F44336",
            NetworkHealthStatus.Disconnected => "#B71C1C",
            _ => "#888888"
        };
        LatencyDisplay = m.AvgLatencyMs.HasValue ? $"{m.AvgLatencyMs.Value:F1} ms" : "—";
        LossDisplay = $"{m.PacketLossPercent:F0}%";
        JitterDisplay = m.JitterMs.HasValue ? $"{m.JitterMs.Value:F1} ms" : "—";

        Measurements.Insert(0, new MeasurementRow(m));
        while (Measurements.Count > 100)
            Measurements.RemoveAt(Measurements.Count - 1);

        LatencyHistory.Add(new LatencyPoint(m));
        while (LatencyHistory.Count > 60)
            LatencyHistory.RemoveAt(0);
    }
}

public class LatencyPoint
{
    public string Time { get; }
    public double Latency { get; }
    public double Loss { get; }
    public double Jitter { get; }
    public double BarWidth { get; }
    public string BarColor { get; }
    public string BarColorEnd { get; }

    public LatencyPoint(NetworkMeasurement m)
    {
        Time = m.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        Latency = m.AvgLatencyMs ?? 0;
        Loss = m.PacketLossPercent;
        Jitter = m.JitterMs ?? 0;
        BarWidth = Math.Min(Math.Max(Latency * 4, 4), 400);

        (BarColor, BarColorEnd) = Latency switch
        {
            <= 20 => ("#10B981", "#06B6D4"),
            <= 50 => ("#F59E0B", "#FB923C"),
            <= 100 => ("#F97316", "#EF4444"),
            _ => ("#EF4444", "#DC2626")
        };
    }
}
