using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.App.Services;
using NetScope.Core.Models;

namespace NetScope.App.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Select a session to view details.";

    [ObservableProperty]
    public partial SessionRow? SelectedSession { get; set; }

    // Aggregate display
    [ObservableProperty]
    public partial bool HasAggregate { get; set; }

    [ObservableProperty]
    public partial string AggMeasurements { get; set; } = "—";

    [ObservableProperty]
    public partial string AggUptime { get; set; } = "—";

    [ObservableProperty]
    public partial string AggLatency { get; set; } = "—";

    [ObservableProperty]
    public partial string AggJitter { get; set; } = "—";

    [ObservableProperty]
    public partial string AggLoss { get; set; } = "—";

    public ObservableCollection<SessionRow> Sessions { get; } = [];
    public ObservableCollection<MeasurementRow> SessionMeasurements { get; } = [];

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        IsLoading = true;
        try
        {
            var sessions = await ServiceLocator.Repository.GetRecentSessionsAsync(50);
            Sessions.Clear();
            foreach (var s in sessions)
                Sessions.Add(new SessionRow(s));

            StatusText = sessions.Count > 0
                ? $"{sessions.Count} session(s) found."
                : "No sessions found. Run a monitor with --save or enable persistence in Monitor tab.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading sessions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectSessionAsync(SessionRow? session)
    {
        SelectedSession = session;
        if (session is null)
        {
            SessionMeasurements.Clear();
            HasAggregate = false;
            return;
        }

        IsLoading = true;
        try
        {
            var measurements = await ServiceLocator.Repository.GetMeasurementsBySessionAsync(session.Id);
            SessionMeasurements.Clear();
            foreach (var m in measurements)
                SessionMeasurements.Add(new MeasurementRow(m));

            var agg = await ServiceLocator.Repository.GetSessionAggregateAsync(session.Id);
            if (agg is not null)
            {
                HasAggregate = true;
                AggMeasurements = agg.TotalMeasurements.ToString();
                AggUptime = $"{agg.UptimePercent:F1}%";
                AggLatency = agg.AvgLatencyMs.HasValue ? $"{agg.AvgLatencyMs.Value:F1} ms" : "—";
                AggJitter = agg.AvgJitterMs.HasValue ? $"{agg.AvgJitterMs.Value:F1} ms" : "—";
                AggLoss = $"{agg.AvgPacketLossPercent:F1}%";
            }
            else
            {
                HasAggregate = false;
            }

            StatusText = $"Session #{session.Id}: {measurements.Count} measurement(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CleanupAsync()
    {
        IsLoading = true;
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            var deleted = await ServiceLocator.Repository.DeleteMeasurementsOlderThanAsync(cutoff);
            var sessions = await ServiceLocator.Repository.DeleteEmptySessionsAsync();
            StatusText = $"Cleanup: {deleted} measurement(s) and {sessions} empty session(s) removed.";
            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Cleanup error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class SessionRow
{
    public long Id { get; }
    public string Started { get; }
    public string Target { get; }
    public int Cycles { get; }
    public string Status { get; }
    public string StatusColor { get; }
    public string Config { get; }

    public SessionRow(MonitoringSession s)
    {
        Id = s.Id;
        Started = s.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        Target = s.Target;
        Cycles = s.MeasurementCount;
        Status = s.CompletedNormally ? "Complete" : s.EndedAt.HasValue ? "Cancelled" : "Running";
        StatusColor = s.CompletedNormally ? "#4CAF50" : s.EndedAt.HasValue ? "#FF9800" : "#2196F3";
        Config = $"{s.IntervalSeconds}s / {s.ProbesPerMeasurement} probes";
    }
}
