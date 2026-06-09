namespace WizConnector.Service.Sage;

public sealed class SageSettings
{
  public string SdkPath { get; set; } = @"C:\Program Files (x86)\Sage Evolution";
  public string CommonConnectionString { get; set; } = string.Empty;
  /// <summary>Default (single-company) connection string. Used when no company alias is specified in a job.</summary>
  public string CompanyConnectionString { get; set; } = string.Empty;
  public string LicenseSerial { get; set; } = string.Empty;
  public string LicenseKey { get; set; } = string.Empty;
  public string AgentUser { get; set; } = "Admin";
  public string AgentPassword { get; set; } = string.Empty;
  public int BranchId { get; set; }
  public bool Enabled { get; set; } = true;

  /// <summary>
  /// MC1 — Multi-company support.
  /// Map of company alias → company connection string.
  /// Example in appsettings.json:
  ///   "Companies": { "ABC Holdings": "Server=...;Database=AcmeLtd;...", "XYZ Corp": "..." }
  /// Jobs pass {"company":"ABC Holdings"} parameter to select a specific company.
  /// Falls back to CompanyConnectionString when alias is absent or not found.
  /// </summary>
  public Dictionary<string, string> Companies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Resolves the connection string for a given company alias (or null for default).</summary>
  public string ResolveCompanyConnectionString(string? companyAlias)
  {
      if (!string.IsNullOrWhiteSpace(companyAlias)
          && Companies.TryGetValue(companyAlias, out var cs)
          && !string.IsNullOrWhiteSpace(cs))
          return cs;
      return CompanyConnectionString;
  }
}
