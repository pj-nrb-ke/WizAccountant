using System.Text.Json;

namespace WizAccountant.Manager;

internal sealed class LauncherSettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5278";
    public string ProductionUrl { get; set; } = "https://app.ascendbooks.biz";

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WizAccountant",
            "pilot-launcher.json");

    public static LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new LauncherSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
