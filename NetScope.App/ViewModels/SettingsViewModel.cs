using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetScope.Infrastructure.Persistence;

namespace NetScope.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string DefaultTarget { get; set; } = "1.1.1.1";

    [ObservableProperty]
    public partial int DefaultInterval { get; set; } = 5;

    [ObservableProperty]
    public partial int DefaultProbes { get; set; } = 4;

    [ObservableProperty]
    public partial int DefaultTimeout { get; set; } = 3000;

    [ObservableProperty]
    public partial string DatabasePath { get; set; } = SqliteMeasurementRepository.GetDefaultDatabasePath();

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [RelayCommand]
    private void SaveDefaults()
    {
        StatusText = "Settings are applied to new monitoring sessions.";
    }
}
