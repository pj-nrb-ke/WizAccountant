using System.Text.Json;

namespace WizAccountant.Manager;

internal sealed class LauncherSettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:8088";
    public string ProductionUrl { get; set; } = "https://app.ascendbooks.biz";

    // ── AppData path (manual saves) ──────────────────────────────────────────
    private static string AppDataPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WizAccountant",
            "pilot-launcher.json");

    /// <summary>
    /// Loads settings using a two-level priority:
    ///   1. pilot.config.json in the repo root  (managed by the dev / AI — always up to date)
    ///   2. AppData pilot-launcher.json          (manual overrides saved via the UI)
    /// Repo config always wins — so changing pilot.config.json takes effect on next launch
    /// without the user touching anything.
    /// </summary>
    public static LauncherSettings Load(string? repoRoot = null)
    {
        // Start from AppData (preserves any manual tweaks the user saved previously)
        var settings = LoadFromAppData();

        // Overlay repo config on top — this is what the AI keeps current
        var repoConfig = LoadFromRepoConfig(repoRoot ?? PilotProcessLauncher.FindRepoRoot());
        if (repoConfig is not null)
        {
            if (!string.IsNullOrWhiteSpace(repoConfig.ApiBaseUrl))
                settings.ApiBaseUrl = repoConfig.ApiBaseUrl;
            if (!string.IsNullOrWhiteSpace(repoConfig.ProductionUrl))
                settings.ProductionUrl = repoConfig.ProductionUrl;
        }

        return settings;
    }

    private static LauncherSettings LoadFromAppData()
    {
        try
        {
            if (!File.Exists(AppDataPath)) return new LauncherSettings();
            var json = File.ReadAllText(AppDataPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch { return new LauncherSettings(); }
    }

    private static LauncherSettings? LoadFromRepoConfig(string? repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot)) return null;
        var path = Path.Combine(repoRoot, "pilot.config.json");
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LauncherSettings>(json);
        }
        catch { return null; }
    }

    /// <summary>Saves current values to AppData (persists manual UI edits).</summary>
    public void Save()
    {
        var dir = Path.GetDirectoryName(AppDataPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(AppDataPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
