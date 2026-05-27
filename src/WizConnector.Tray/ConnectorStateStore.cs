using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Tray;

internal static class ConnectorStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ConnectorStateDto? Load()
    {
        if (!File.Exists(ConnectorPaths.StateFilePath)) return null;
        var json = File.ReadAllText(ConnectorPaths.StateFilePath);
        return JsonSerializer.Deserialize<ConnectorStateDto>(json, JsonOptions);
    }

    public static void Save(ConnectorStateDto state)
    {
        Directory.CreateDirectory(ConnectorPaths.ConfigFolder);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(ConnectorPaths.StateFilePath, json);
    }

    public static void Clear()
    {
        if (File.Exists(ConnectorPaths.StateFilePath))
            File.Delete(ConnectorPaths.StateFilePath);
    }
}
