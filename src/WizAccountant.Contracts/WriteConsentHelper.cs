using System.Text.Json;

namespace WizAccountant.Contracts;

public static class WriteConsentHelper
{
    private static readonly string Path = System.IO.Path.Combine(ConnectorPaths.ConfigFolder, "write-consent.json");

    public static bool IsAllowed()
    {
        if (!File.Exists(Path)) return false;
        try
        {
            var doc = JsonSerializer.Deserialize<ConsentDoc>(File.ReadAllText(Path));
            return doc?.AllowedUntilUtc > DateTimeOffset.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    public static void Grant(TimeSpan duration)
    {
        Directory.CreateDirectory(ConnectorPaths.ConfigFolder);
        var doc = new ConsentDoc { AllowedUntilUtc = DateTimeOffset.UtcNow.Add(duration) };
        File.WriteAllText(Path, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class ConsentDoc
    {
        public DateTimeOffset AllowedUntilUtc { get; set; }
    }
}
