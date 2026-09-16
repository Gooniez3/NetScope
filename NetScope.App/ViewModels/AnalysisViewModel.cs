using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.App.Services;
using NetScope.Core.Diagnostics;
using NetScope.Core.Models;

namespace NetScope.App.ViewModels;

public partial class AnalysisViewModel : ViewModelBase
{
    private readonly Action<string> _runDiagnostics;
    private readonly IDiagnosticAnalyzer _analyzer = ServiceLocator.DiagnosticAnalyzer;
    private long _analyzedSessionId = -1;

    public AnalysisViewModel(Action<string> runDiagnostics)
    {
        _runDiagnostics = runDiagnostics;
        SelectedScope = Scopes[0];
    }

    public IReadOnlyList<string> Scopes { get; } =
    [
        "Selected session",
        "Last 1 hour",
        "Last 6 hours",
        "Last 24 hours"
    ];

    public ObservableCollection<SessionRow> Sessions { get; } = [];
    public ObservableCollection<FindingRow> Findings { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial string SelectedScope { get; set; } = "Selected session";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    public partial SessionRow? SelectedSession { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasReport { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Select a session or time range, then analyze.";

    [ObservableProperty]
    public partial string Headline { get; set; } = "";

    [ObservableProperty]
    public partial string SeverityText { get; set; } = "";

    [ObservableProperty]
    public partial string SeverityColor { get; set; } = "#8B949E";

    [ObservableProperty]
    public partial string MetaLine { get; set; } = "";

    [ObservableProperty]
    public partial bool HasMetrics { get; set; }

    [ObservableProperty]
    public partial string MetricLatency { get; set; } = "—";

    [ObservableProperty]
    public partial string MetricLoss { get; set; } = "—";

    [ObservableProperty]
    public partial string MetricJitter { get; set; } = "—";

    [ObservableProperty]
    public partial bool HasFindings { get; set; }

    [ObservableProperty]
    public partial string Target { get; set; } = "";

    [ObservableProperty]
    public partial bool CanRunDiagnostics { get; set; }

    [ObservableProperty]
    public partial string RunDiagnosticsReason { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowLocalNetworkHint { get; set; }

    [ObservableProperty]
    public partial string LocalNetworkHint { get; set; } = "";

    public string PageIntro { get; } =
        "Grades a saved monitoring run. Dashboard is the latest ping; this page is the whole session.";

    public bool IsSessionScope => SelectedScope == "Selected session";

    [ObservableProperty]
    public partial bool HasOutcomeNote { get; set; }

    [ObservableProperty]
    public partial string OutcomeNote { get; set; } = "";

    partial void OnSelectedScopeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSessionScope));
        if (!IsSessionScope)
            _ = AnalyzeCommand.ExecuteAsync(null);
    }

    partial void OnSelectedSessionChanged(SessionRow? value)
    {
        if (!IsSessionScope || value is null)
            return;
        if (value.Id == _analyzedSessionId && HasReport)
            return;
        _ = AnalyzeCommand.ExecuteAsync(null);
    }

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
                ? $"{sessions.Count} session(s) available."
                : "No saved sessions. Run Monitor with Save enabled, then return here.";
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

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<NetworkMeasurement> measurements;

            if (IsSessionScope)
            {
                if (SelectedSession is null)
                {
                    ClearReport("Select a session to analyze.");
                    return;
                }

                measurements = await ServiceLocator.Repository.GetMeasurementsBySessionAsync(SelectedSession.Id);
                _analyzedSessionId = SelectedSession.Id;
            }
            else
            {
                var hours = SelectedScope switch
                {
                    "Last 1 hour" => 1,
                    "Last 6 hours" => 6,
                    _ => 24
                };
                var to = DateTimeOffset.UtcNow;
                measurements = await ServiceLocator.Repository.GetMeasurementsByTimeRangeAsync(to.AddHours(-hours), to);
                _analyzedSessionId = -1;
            }

            ApplyReport(_analyzer.Analyze(new DiagnosticRequest { Measurements = measurements }));
        }
        catch (Exception ex)
        {
            ClearReport($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void RunDiagnostics()
    {
        if (string.IsNullOrWhiteSpace(Target))
            return;
        _runDiagnostics(Target);
    }

    private bool CanAnalyze() => !IsSessionScope || SelectedSession is not null;

    private void ApplyReport(DiagnosticReport report)
    {
        HasReport = true;
        Headline = report.Headline;
        SeverityText = report.Severity.ToString();
        SeverityColor = report.Severity switch
        {
            DiagnosticSeverity.Info => "#3FB950",
            DiagnosticSeverity.Warning => "#D29922",
            DiagnosticSeverity.Alert => "#F85149",
            DiagnosticSeverity.Critical => "#F85149",
            _ => "#8B949E"
        };
        Target = report.Target;

        MetaLine = FormatMeta(report);
        HasMetrics = report.SampleCount > 0;
        MetricLatency = report.AvgLatencyMs.HasValue ? $"{report.AvgLatencyMs.Value:F1} ms" : "—";
        MetricLoss = report.SampleCount > 0 ? $"{report.AvgPacketLossPercent:F1}%" : "—";
        MetricJitter = report.AvgJitterMs.HasValue ? $"{report.AvgJitterMs.Value:F1} ms" : "—";

        Findings.Clear();
        var sparse = report.Findings.FirstOrDefault(f => f.Code == "DATA_SPARSE");
        var issues = report.Findings
            .Where(f => f.Code is not "HEALTHY" and not "DATA_SPARSE")
            .ToList();
        var snapshotOnly = issues.Count > 0 && issues.All(IsSnapshotElevated);

        if (!snapshotOnly)
        {
            foreach (var f in issues)
                Findings.Add(new FindingRow(f));
        }

        HasFindings = Findings.Count > 0;
        OutcomeNote = BuildOutcomeNote(report, sparse, issues);
        HasOutcomeNote = report.SampleCount > 0 && OutcomeNote.Length > 0;

        var run = report.SuggestedActions.FirstOrDefault(a => a.Kind == DiagnosticActionKind.RunDiagnostics);
        CanRunDiagnostics = run is not null && !string.IsNullOrWhiteSpace(report.Target);
        RunDiagnosticsReason = run?.Reason ?? "";

        var local = report.SuggestedActions.FirstOrDefault(a => a.Kind == DiagnosticActionKind.CheckLocalNetwork);
        ShowLocalNetworkHint = local is not null;
        LocalNetworkHint = local?.Reason ?? "";

        StatusText = report.SampleCount == 0
            ? report.Headline
            : $"Analyzed {report.SampleCount} measurement(s).";
    }

    private void ClearReport(string status)
    {
        HasReport = false;
        Headline = "";
        Findings.Clear();
        HasFindings = false;
        HasOutcomeNote = false;
        OutcomeNote = "";
        HasMetrics = false;
        CanRunDiagnostics = false;
        ShowLocalNetworkHint = false;
        StatusText = status;
    }

    private static string BuildOutcomeNote(
        DiagnosticReport report,
        DiagnosticFinding? sparse,
        IReadOnlyList<DiagnosticFinding> issues)
    {
        var shortRun = sparse is not null ? "This run is too short to call a trend." : null;

        if (issues.Count == 0)
        {
            return shortRun is null
                ? "Averages are within healthy limits. No worsening trend across this session."
                : "Averages are within healthy limits. " + shortRun;
        }

        if (issues.All(IsSnapshotElevated))
        {
            var names = new List<string>();
            if (issues.Any(f => f.Code == "LOSS_ELEVATED"))
                names.Add("packet loss");
            if (issues.Any(f => f.Code == "LATENCY_ELEVATED"))
                names.Add("latency");
            if (issues.Any(f => f.Code == "JITTER_ELEVATED"))
                names.Add("jitter");

            var subject = JoinEnglish(names);
            var verb = names.Count == 1 ? "is" : "are";
            var note = $"{Capitalize(subject)} {verb} above the warning line.";

            if (issues.Any(f => f.Code == "JITTER_ELEVATED")
                && issues.All(f => f.Code != "LOSS_ELEVATED")
                && report.AvgPacketLossPercent < 0.05)
            {
                note += " Packet loss is still 0%, so this is latency instability, not drop.";
            }

            if (shortRun is not null)
                note += " " + shortRun;

            return note;
        }

        return shortRun ?? "";
    }

    private static bool IsSnapshotElevated(DiagnosticFinding f) =>
        f.Code is "JITTER_ELEVATED" or "LATENCY_ELEVATED" or "LOSS_ELEVATED";

    private static string JoinEnglish(IReadOnlyList<string> parts) =>
        parts.Count switch
        {
            0 => "",
            1 => parts[0],
            2 => $"{parts[0]} and {parts[1]}",
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + ", and " + parts[^1]
        };

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string FormatMeta(DiagnosticReport report)
    {
        if (report.SampleCount == 0)
            return "No samples";

        var period = "—";
        if (report.PeriodStart.HasValue && report.PeriodEnd.HasValue)
        {
            var start = report.PeriodStart.Value.ToLocalTime();
            var end = report.PeriodEnd.Value.ToLocalTime();
            var format = report.AnalyzedDuration < TimeSpan.FromMinutes(2) ? "HH:mm:ss" : "HH:mm";
            period = $"{start.ToString(format)} → {end.ToString(format)}";
        }

        return $"{report.SampleCount} samples  /  {report.Target}  /  {period}";
    }
}

public class FindingRow
{
    public string Severity { get; }
    public string SeverityColor { get; }
    public string Title { get; }
    public string Detail { get; }

    public FindingRow(DiagnosticFinding f)
    {
        Severity = f.Severity.ToString();
        SeverityColor = f.Severity switch
        {
            DiagnosticSeverity.Info => "#3FB950",
            DiagnosticSeverity.Warning => "#D29922",
            DiagnosticSeverity.Alert => "#F85149",
            DiagnosticSeverity.Critical => "#F85149",
            _ => "#8B949E"
        };
        Title = f.Title;
        Detail = f.Code switch
        {
            "JITTER_ELEVATED" => "Above the warning line for this session.",
            "LATENCY_ELEVATED" => "Above the warning line for this session.",
            "LOSS_ELEVATED" => "Above the warning line for this session.",
            _ => f.Detail
        };
    }
}
