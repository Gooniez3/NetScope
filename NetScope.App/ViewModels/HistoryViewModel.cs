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
    [NotifyCanExecuteChangedFor(nameof(DeleteSessionCommand))]
    public partial SessionRow? SelectedSession { get; set; }

    [ObservableProperty]
    public partial bool ConfirmingClearAll { get; set; }

    private long _loadedSessionId = -1;

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

    [ObservableProperty]
    public partial string DetailTitle { get; set; } = "No session selected";

    public ObservableCollection<SessionRow> Sessions { get; } = [];
    public ObservableCollection<MeasurementRow> SessionMeasurements { get; } = [];

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        await ReloadSessionsAsync();
        StatusText = Sessions.Count > 0
            ? $"{Sessions.Count} session(s). Select one to inspect."
            : "No sessions found. Enable Save on Monitor, then run a session.";
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSession))]
    private async Task DeleteSessionAsync()
    {
        if (SelectedSession is null)
            return;

        var id = SelectedSession.Id;
        IsLoading = true;
        ConfirmingClearAll = false;
        try
        {
            var measurements = await ServiceLocator.Repository.DeleteSessionAsync(id);
            ResetDetail();
            await ReloadSessionsAsync();
            StatusText = $"Deleted session #{id} ({measurements} measurement(s)).";
        }
        catch (Exception ex)
        {
            StatusText = $"Delete error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        if (Sessions.Count == 0)
        {
            StatusText = "Nothing to clear.";
            return;
        }

        if (!ConfirmingClearAll)
        {
            ConfirmingClearAll = true;
            StatusText = "Click Clear all again to permanently delete every session.";
            return;
        }

        IsLoading = true;
        try
        {
            var count = await ServiceLocator.Repository.DeleteAllAsync();
            ConfirmingClearAll = false;
            ResetDetail();
            await ReloadSessionsAsync();
            StatusText = $"Cleared {count} session(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Clear error: {ex.Message}";
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
        ConfirmingClearAll = false;
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            var deleted = await ServiceLocator.Repository.DeleteMeasurementsOlderThanAsync(cutoff);
            var sessions = await ServiceLocator.Repository.DeleteEmptySessionsAsync();
            await ReloadSessionsAsync();
            StatusText = deleted == 0 && sessions == 0
                ? "Nothing older than 30 days. Use Delete session or Clear all for recent data."
                : $"Removed {deleted} measurement(s) and {sessions} old session(s).";
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

    partial void OnSelectedSessionChanged(SessionRow? value)
    {
        ConfirmingClearAll = false;
        if (value is null)
        {
            ResetDetail();
            return;
        }

        if (value.Id == _loadedSessionId)
            return;

        _ = LoadDetailAsync(value);
    }

    private bool CanDeleteSession() => SelectedSession is not null;

    private async Task ReloadSessionsAsync()
    {
        IsLoading = true;
        try
        {
            var sessions = await ServiceLocator.Repository.GetRecentSessionsAsync(50);
            Sessions.Clear();
            foreach (var s in sessions)
                Sessions.Add(new SessionRow(s));
            _loadedSessionId = -1;
            DeleteSessionCommand.NotifyCanExecuteChanged();
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

    private async Task LoadDetailAsync(SessionRow session)
    {
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

            _loadedSessionId = session.Id;
            DetailTitle = $"Session #{session.Id}  ·  {session.Target}";
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

    private void ResetDetail()
    {
        SessionMeasurements.Clear();
        HasAggregate = false;
        DetailTitle = "No session selected";
        _loadedSessionId = -1;
    }
}

public class SessionRow
{
    public long Id { get; }
    public string Started { get; }
    public string StartedShort { get; }
    public string Target { get; }
    public int Cycles { get; }
    public string CyclesLabel { get; }
    public string Status { get; }
    public string StatusColor { get; }
    public string Config { get; }
    public string Display { get; }

    public SessionRow(MonitoringSession s)
    {
        Id = s.Id;
        Started = s.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        StartedShort = s.StartedAt.ToLocalTime().ToString("MMM d HH:mm");
        Target = s.Target;
        Cycles = s.MeasurementCount;
        CyclesLabel = $"{s.MeasurementCount}";
        Status = s.CompletedNormally ? "Complete" : s.EndedAt.HasValue ? "Cancelled" : "Running";
        StatusColor = s.CompletedNormally ? "#3FB950" : s.EndedAt.HasValue ? "#D29922" : "#58A6FF";
        Config = $"{s.IntervalSeconds}s / {s.ProbesPerMeasurement} probes";
        Display = $"#{Id}  {Started}  {Target}";
    }
}
