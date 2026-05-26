using System.Text.Json;

namespace WizAccountant.Contracts;

/// <summary>
/// Sage connection settings entered via WizConnector.Setup.exe (stored encrypted on disk).
/// </summary>
public sealed class SageConnectorConfig
{
    public string Server { get; set; } = string.Empty;
    public string CompanyDatabase { get; set; } = string.Empty;
    public string CommonDatabase { get; set; } = string.Empty;
    public bool UseWindowsAuthentication { get; set; }
    public string SqlUser { get; set; } = string.Empty;
    public string SqlPassword { get; set; } = string.Empty;
    public string LicenseSerial { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public string AgentUser { get; set; } = "Admin";
    public string AgentPassword { get; set; } = string.Empty;
    public int BranchId { get; set; }

    public string BuildCompanyConnectionString()
    {
        return BuildConnectionString(CompanyDatabase);
    }

    public string BuildCommonConnectionString()
    {
        return BuildConnectionString(CommonDatabase);
    }

    private string BuildConnectionString(string database)
    {
        if (UseWindowsAuthentication)
        {
            return $"Data Source={Server};Initial Catalog={database};Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
        }

        return $"Data Source={Server};Initial Catalog={database};User ID={SqlUser};Password={SqlPassword};TrustServerCertificate=True;Encrypt=False";
    }

    public Dictionary<string, string?> ToConfigurationDictionary()
    {
        return new Dictionary<string, string?>
        {
            ["Sage:Enabled"] = "true",
            ["Sage:CompanyConnectionString"] = BuildCompanyConnectionString(),
            ["Sage:CommonConnectionString"] = BuildCommonConnectionString(),
            ["Sage:LicenseSerial"] = LicenseSerial,
            ["Sage:LicenseKey"] = LicenseKey,
            ["Sage:AgentUser"] = AgentUser,
            ["Sage:AgentPassword"] = AgentPassword,
            ["Sage:BranchId"] = BranchId.ToString()
        };
    }
}

public static class SageConfigStorage
{
    public const string UserSecretsId = "dotnet-WizConnector.Service-7a870d9f-c3fd-49f5-82b2-c2003f67f9f2";

    private static readonly byte[] Entropy = "WizAccountant.Sage.v1"u8.ToArray();

    public static string ConfigFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WizConnector");

    public static string EncryptedFilePath => Path.Combine(ConfigFolder, "sage.config");

    public static string UserSecretsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", UserSecretsId, "secrets.json");

    public static void SaveEncrypted(SageConnectorConfig config)
    {
        Directory.CreateDirectory(ConfigFolder);
        var json = JsonSerializer.Serialize(config);
        var plain = System.Text.Encoding.UTF8.GetBytes(json);
        var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
            plain, Entropy, System.Security.Cryptography.DataProtectionScope.LocalMachine);
        File.WriteAllBytes(EncryptedFilePath, protectedBytes);
    }

    public static SageConnectorConfig? LoadEncrypted()
    {
        if (!File.Exists(EncryptedFilePath)) return null;

        var protectedBytes = File.ReadAllBytes(EncryptedFilePath);
        var plain = System.Security.Cryptography.ProtectedData.Unprotect(
            protectedBytes, Entropy, System.Security.Cryptography.DataProtectionScope.LocalMachine);
        var json = System.Text.Encoding.UTF8.GetString(plain);
        return JsonSerializer.Deserialize<SageConnectorConfig>(json);
    }

    public static void SaveUserSecrets(SageConnectorConfig config)
    {
        var folder = Path.GetDirectoryName(UserSecretsFilePath)!;
        Directory.CreateDirectory(folder);

        var dict = config.ToConfigurationDictionary();
        var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(UserSecretsFilePath, json);
    }
}
