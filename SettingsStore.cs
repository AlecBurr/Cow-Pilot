using System.Text.Json;

namespace CowPilot;

static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppVersion.Name);
    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return DefaultSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? DefaultSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return DefaultSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static AppSettings Clone(AppSettings settings)
    {
        settings.Normalize();
        var clone = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, JsonOptions), JsonOptions) ?? DefaultSettings();
        clone.Normalize();
        return clone;
    }

    public static AppSettings DefaultSettings()
    {
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }
}
