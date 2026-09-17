using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.App.Services;
using NetScope.Core.Models;

namespace NetScope.App.ViewModels;

public partial class MonitorViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Target { get; set; } = AppDefaults.Target;

    [ObservableProperty]
    public partial int IntervalSeconds { get; set; } = AppDefaults.IntervalSeconds;

    [ObservableProperty]
    public partial int ProbesPerCycle { get; set; } = AppDefaults.ProbesPerCycle;

    [ObservableProperty]
    public partial int TimeoutMs { get; set; } = AppDefaults.TimeoutMs;

    [ObservableProperty]
    public partial bool SaveToDatabase { get; set; } = true;

    [ObservableProperty]
    public partial bool IsMonitoring { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Idle";

    [ObservableProperty]
    public partial string HealthStatus { get; set; } = "Idle";

    [ObservableProperty]
    public partial string HealthColor { get; set; } = "#8B949E";

    [ObservableProperty]
    public partial string LatencyDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial string LossDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial string JitterDisplay { get; set; } = "—";

    [ObservableProperty]
    public partial int CycleCount { get; set; }

    public ObservableCollection<MeasurementRow> Measurements { get; } = [];

    private readonly List<double> _chartTimestamps = [];
    private readonly List<double> _chartLatencies = [];
    public IReadOnlyList<double> ChartTimestamps => _chartTimestamps;
    public IReadOnlyList<double> ChartLatencies => _chartLatencies;
    public event Action? ChartUpdated;
    public event Action? ChartCleared;

    private CancellationTokenSource? _cts;
    private long _sessionId;

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        CycleCount = 0;
        HealthStatus = "Idle";
        HealthColor = "#8B949E";
        LatencyDisplay = "—";
        LossDisplay = "—";
        JitterDisplay = "—";
        Measurements.Clear();
        _chartTimestamps.Clear();
        _chartLatencies.Clear();
        ChartCleared?.Invoke();
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
        catch (ArgumentException ex)
        {
            StatusText = $"Invalid settings: {ex.Message}";
            return;
        }
        finally
        {
            if (SaveToDatabase && _sessionId > 0)
            {
                try
                {
                    await ServiceLocator.Repository.CompleteSessionAsync(
                        _sessionId, DateTimeOffset.UtcNow, CycleCount, !_cts!.IsCancellationRequested);
                }
                catch { }
            }

            IsMonitoring = false;
            if (!StatusText.StartsWith("Invalid settings", StringComparison.Ordinal))
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
            NetworkHealthStatus.Healthy => "#3FB950",
            NetworkHealthStatus.Degraded => "#D29922",
            NetworkHealthStatus.Unstable => "#F85149",
            NetworkHealthStatus.Disconnected => "#F85149",
            _ => "#484F58"
        };
        LatencyDisplay = m.AvgLatencyMs.HasValue ? $"{m.AvgLatencyMs.Value:F1} ms" : "—";
        LossDisplay = $"{m.PacketLossPercent:F0}%";
        JitterDisplay = m.JitterMs.HasValue ? $"{m.JitterMs.Value:F1} ms" : "—";

        Measurements.Insert(0, new MeasurementRow(m));
        while (Measurements.Count > 100)
            Measurements.RemoveAt(Measurements.Count - 1);

        _chartTimestamps.Add(m.Timestamp.LocalDateTime.ToOADate());
        _chartLatencies.Add(m.AvgLatencyMs ?? 0);
        while (_chartTimestamps.Count > 120)
        {
            _chartTimestamps.RemoveAt(0);
            _chartLatencies.RemoveAt(0);
        }
        ChartUpdated?.Invoke();
    }
}
