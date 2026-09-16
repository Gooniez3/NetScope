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
    public partial string DatabasePath { get; set; } = SqliteMeasurementRepository.GetDefaultDatabasePath();

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
        StatusText = "Defaults saved. New monitoring sessions will use these values.";
    }
}

/// <summary>
/// Simple in-memory defaults shared across ViewModels.
/// Not persisted to disk — resets on app restart.
/// </summary>
public static class AppDefaults
{
    public static string Target { get; set; } = "1.1.1.1";
    public static int IntervalSeconds { get; set; } = 5;
    public static int ProbesPerCycle { get; set; } = 4;
    public static int TimeoutMs { get; set; } = 3000;
}
