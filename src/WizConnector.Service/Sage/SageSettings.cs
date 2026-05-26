namespace WizConnector.Service.Sage;

public sealed class SageSettings
{
  public string SdkPath { get; set; } = @"C:\Program Files (x86)\Sage Evolution";
  public string CommonConnectionString { get; set; } = string.Empty;
  public string CompanyConnectionString { get; set; } = string.Empty;
  public string LicenseSerial { get; set; } = string.Empty;
  public string LicenseKey { get; set; } = string.Empty;
  public string AgentUser { get; set; } = "Admin";
  public string AgentPassword { get; set; } = string.Empty;
  public int BranchId { get; set; }
  public bool Enabled { get; set; } = true;
}
