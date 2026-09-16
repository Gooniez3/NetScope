using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.Core.Models;
using NetScope.Infrastructure.Persistence;

namespace NetScope.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string DefaultTarget { get; set; } = AppDefaults.Target;

    [ObservableProperty]
    public partial int DefaultInterval { get; set; } = AppDefaults.IntervalSeconds;

    [ObservableProperty]
    public partial int DefaultProbes { get; set; } = AppDefaults.ProbesPerCycle;

    [ObservableProperty]
    public partial int DefaultTimeout { get; set; } = AppDefaults.TimeoutMs;

    [ObservableProperty]
    public partial bool CloseToTray { get; set; } = AppDefaults.CloseToTray;

    [ObservableProperty]
    public partial string DatabasePath { get; set; } = SqliteMeasurementRepository.GetDefaultDatabasePath();

    [ObservableProperty]
    public partial string SettingsPath { get; set; } = Services.AppSettingsStore.GetDefaultPath();

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [RelayCommand]
    private void SaveDefaults()
    {
        try
        {
            new MonitorOptions
            {
                Target = DefaultTarget,
                IntervalSeconds = DefaultInterval,
                ProbesPerMeasurement = DefaultProbes,
                TimeoutMs = DefaultTimeout
            }.Validate();
        }
        catch (ArgumentException ex)
        {
            StatusText = ex.Message;
            return;
        }

        AppDefaults.Target = DefaultTarget;
        AppDefaults.IntervalSeconds = DefaultInterval;
        AppDefaults.ProbesPerCycle = DefaultProbes;
        AppDefaults.TimeoutMs = DefaultTimeout;
        AppDefaults.CloseToTray = CloseToTray;
        AppDefaults.Save();
        StatusText = "Saved to disk. New monitoring sessions will use these values.";
    }
}
