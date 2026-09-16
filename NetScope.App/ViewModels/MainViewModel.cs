using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NetScope.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
    [NotifyPropertyChangedFor(nameof(IsMonitorActive))]
    [NotifyPropertyChangedFor(nameof(IsHistoryActive))]
    [NotifyPropertyChangedFor(nameof(IsDiagnosticsActive))]
    [NotifyPropertyChangedFor(nameof(IsLanDiscoveryActive))]
    [NotifyPropertyChangedFor(nameof(IsAnalysisActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    public partial string SelectedNav { get; set; } = "Dashboard";

    public bool IsDashboardActive => SelectedNav == "Dashboard";
    public bool IsMonitorActive => SelectedNav == "Monitor";
    public bool IsHistoryActive => SelectedNav == "History";
    public bool IsDiagnosticsActive => SelectedNav == "Diagnostics";
    public bool IsLanDiscoveryActive => SelectedNav == "LanDiscovery";
    public bool IsAnalysisActive => SelectedNav == "Analysis";
    public bool IsSettingsActive => SelectedNav == "Settings";

    public DashboardViewModel Dashboard { get; }
    public MonitorViewModel Monitor { get; }
    public HistoryViewModel History { get; } = new();
    public DiagnosticsViewModel Diagnostics { get; } = new();
    public AnalysisViewModel Analysis { get; }
    public SettingsViewModel Settings { get; } = new();

    public MainViewModel()
    {
        Monitor = new MonitorViewModel();
        Dashboard = new DashboardViewModel(Monitor, () => Navigate("Monitor"));
        Analysis = new AnalysisViewModel(target =>
        {
            Diagnostics.PrepareTarget(target);
            Navigate("Diagnostics");
        });
        CurrentPage = Dashboard;
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        SelectedNav = page;
        CurrentPage = page switch
        {
            "Dashboard" => Dashboard,
            "Monitor" => Monitor,
            "History" => History,
            "Diagnostics" => OpenDiagnostics(0),
            "LanDiscovery" => OpenDiagnostics(4),
            "Analysis" => Analysis,
            "Settings" => Settings,
            _ => Dashboard
        };
    }

    private DiagnosticsViewModel OpenDiagnostics(int tabIndex)
    {
        Diagnostics.SelectedTabIndex = tabIndex;
        return Diagnostics;
    }
}
