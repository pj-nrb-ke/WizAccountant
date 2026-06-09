using System.Text.Json;
using System.Text.Json.Serialization;

namespace WizConnector.Tray;

internal sealed class TraySettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:8088";

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WizConnector", "tray-settings.json");

    /// <summary>
    /// Loads with two-level priority — same pattern as WizPilot:
    ///   1. pilot.config.json in the repo root  (AI keeps this current)
    ///   2. AppData tray-settings.json          (manual saves via Status window)
    /// </summary>
    public static TraySettings Load()
    {
        var settings = LoadFromAppData();
        var repoConfig = LoadFromPilotConfig();
        if (repoConfig is not null && !string.IsNullOrWhiteSpace(repoConfig.ApiBaseUrl))
            settings.ApiBaseUrl = repoConfig.ApiBaseUrl;
        return settings;
    }

    private static TraySettings LoadFromAppData()
    {
        try
        {
            if (!File.Exists(FilePath)) return new TraySettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<TraySettings>(json) ?? new TraySettings();
        }
        catch { return new TraySettings(); }
    }

    private static TraySettings? LoadFromPilotConfig()
    {
        // Walk up from the exe location to find the repo root (contains WizAccountant.slnx)
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "pilot.config.json");
            if (File.Exists(candidate))
            {
                try
                {
                    var json = File.ReadAllText(candidate);
                    var cfg = JsonSerializer.Deserialize<PilotConfig>(json);
                    if (cfg is not null)
                        return new TraySettings { ApiBaseUrl = cfg.ApiBaseUrl ?? ApiBaseUrlDefault };
                }
                catch { }
            }
            // Also check known hardcoded dev path
            var devPath = Path.Combine(@"C:\Users\pj\WizAccountant", "pilot.config.json");
            if (File.Exists(devPath))
            {
                try
                {
                    var json = File.ReadAllText(devPath);
                    var cfg = JsonSerializer.Deserialize<PilotConfig>(json);
                    if (cfg is not null)
                        return new TraySettings { ApiBaseUrl = cfg.ApiBaseUrl ?? ApiBaseUrlDefault };
                }
                catch { }
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    public void Save()
    {
        var d = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(d);
        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private const string ApiBaseUrlDefault = "http://localhost:8088";

    private sealed class PilotConfig
    {
        [JsonPropertyName("ApiBaseUrl")]
        public string? ApiBaseUrl { get; set; }
    }
}
