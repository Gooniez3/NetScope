using System.Text.Json;

namespace NetScope.App.Services;

/// <summary>
/// JSON settings next to the SQLite database: <c>~/.netscope/settings.json</c>.
/// Missing or invalid files fall back to defaults.
/// </summary>
public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetDefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".netscope");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    public static AppSettings Load()
    {
        try
        {
            var path = GetDefaultPath();
            if (!File.Exists(path))
                return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var path = GetDefaultPath();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }
}

public sealed class AppSettings
{
    public string Target { get; set; } = "1.1.1.1";
    public int IntervalSeconds { get; set; } = 5;
    public int ProbesPerCycle { get; set; } = 4;
    public int TimeoutMs { get; set; } = 3000;
    public bool CloseToTray { get; set; }
}
