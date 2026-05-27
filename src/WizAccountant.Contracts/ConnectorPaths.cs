namespace WizAccountant.Contracts;

public static class ConnectorPaths
{
    public static string ConfigFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WizConnector");

    public static string StateFilePath => Path.Combine(ConfigFolder, "connector-state.json");

    public static string SageConfigFilePath => Path.Combine(ConfigFolder, "sage.config");
}

public sealed class ConnectorStateDto
{
    public Guid SiteId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
}
