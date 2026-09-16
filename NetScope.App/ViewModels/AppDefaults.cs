using Avalonia;
using Avalonia.Styling;
using NetScope.App.Services;
using NetScope.Core.Models;

namespace NetScope.App.ViewModels;

/// <summary>
/// Shared application defaults persisted to <c>~/.netscope/settings.json</c>.
/// </summary>
public static class AppDefaults
{
    public static string Target { get; set; } = "1.1.1.1";
    public static int IntervalSeconds { get; set; } = 5;
    public static int ProbesPerCycle { get; set; } = 4;
    public static int TimeoutMs { get; set; } = 3000;
    public static bool CloseToTray { get; set; }

    public static void Load()
    {
        var s = AppSettingsStore.Load();
        Target = string.IsNullOrWhiteSpace(s.Target) ? "1.1.1.1" : s.Target.Trim();
        IntervalSeconds = s.IntervalSeconds;
        ProbesPerCycle = s.ProbesPerCycle;
        TimeoutMs = s.TimeoutMs;
        CloseToTray = s.CloseToTray;
        ApplyDarkTheme();
    }

    public static void Save()
    {
        AppSettingsStore.Save(new AppSettings
        {
            Target = Target,
            IntervalSeconds = IntervalSeconds,
            ProbesPerCycle = ProbesPerCycle,
            TimeoutMs = TimeoutMs,
            CloseToTray = CloseToTray
        });
        ApplyDarkTheme();
    }

    public static void ApplyDarkTheme()
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
    }

    public static void ValidateMonitorOrThrow()
    {
        new MonitorOptions
        {
            Target = Target,
            IntervalSeconds = IntervalSeconds,
            ProbesPerMeasurement = ProbesPerCycle,
            TimeoutMs = TimeoutMs
        }.Validate();
    }
}
