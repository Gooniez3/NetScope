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
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    public partial string SelectedNav { get; set; } = "Dashboard";

    public bool IsDashboardActive => SelectedNav == "Dashboard";
    public bool IsMonitorActive => SelectedNav == "Monitor";
    public bool IsHistoryActive => SelectedNav == "History";
    public bool IsDiagnosticsActive => SelectedNav == "Diagnostics";
    public bool IsSettingsActive => SelectedNav == "Settings";

    public DashboardViewModel Dashboard { get; } = new();
    public MonitorViewModel Monitor { get; } = new();
    public HistoryViewModel History { get; } = new();
    public DiagnosticsViewModel Diagnostics { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    public MainViewModel()
    {
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
            "Diagnostics" => Diagnostics,
            "Settings" => Settings,
            _ => Dashboard
        };
    }
}
