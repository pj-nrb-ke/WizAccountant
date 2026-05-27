using System.Text.Json;

namespace WizConnector.Tray;

internal sealed class TraySettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5278";

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WizConnector", "tray-settings.json");

    public static TraySettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new TraySettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<TraySettings>(json) ?? new TraySettings();
        }
        catch
        {
            return new TraySettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
