using System.Data.SqlClient;
using System.Text.Json;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Returns connector version, SDK version, and schema capability facts for this site.
/// Combines GAP-020 (SQL proof) and GAP-021 (capability discovery) in a single endpoint.
/// </summary>
internal static class SiteMetadataHandler
{
    /// <summary>Key tables the connector relies on — their presence is the schema proof.</summary>
    private static readonly string[] KeyTables =
    [
        "Client", "Vendor", "PostAR", "PostAP", "PostGL",
        "InvNum", "Accounts", "StkItem"
    ];

    /// <summary>MC1 — return list of configured company aliases.</summary>
    public static string ExecuteCompanyList(SageSettings settings)
    {
        var companies = new List<object>();
        if (!string.IsNullOrWhiteSpace(settings.CompanyConnectionString))
            companies.Add(new { alias = "(default)", database = ParseConnectionCatalog(settings.CompanyConnectionString) });
        foreach (var (alias, cs) in settings.Companies)
            companies.Add(new { alias, database = ParseConnectionCatalog(cs) });

        return System.Text.Json.JsonSerializer.Serialize(new { companies });
    }

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        // Batch-check key table presence via INFORMATION_SCHEMA.TABLES
        var paramNames = Enumerable.Range(0, KeyTables.Length).Select(i => $"@t{i}").ToArray();
        var sql = $"""
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME IN ({string.Join(",", paramNames)})
            """;

        HashSet<string> confirmed;
        string? schemaError = null;
        try
        {
            var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
            {
                for (var i = 0; i < KeyTables.Length; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], KeyTables[i]);
            });
            confirmed = new HashSet<string>(
                rows.Select(r => r["TABLE_NAME"]?.ToString() ?? "").Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            confirmed = [];
            schemaError = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
        }

        var schemaStatus = KeyTables.Select(t => new { tableName = t, present = confirmed.Contains(t) }).ToList();
        var allPresent = schemaStatus.All(s => s.present);

        var capabilities = new
        {
            arSupported = confirmed.Contains("PostAR") && confirmed.Contains("Client"),
            apSupported = confirmed.Contains("PostAP") && confirmed.Contains("Vendor"),
            glSupported = confirmed.Contains("PostGL") && confirmed.Contains("Accounts"),
            invoicingSupported = confirmed.Contains("InvNum"),
            inventorySupported = confirmed.Contains("StkItem")
        };

        return JsonSerializer.Serialize(new
        {
            connectorVersion = typeof(SageSdkJobExecutor).Assembly.GetName().Version?.ToString(),
            sdkVersion = typeof(DatabaseContext).Assembly.GetName().Version?.ToString(),
            companyDatabase = ParseConnectionCatalog(connectionString),
            handlerCount = 115, // approximate — keep in sync with SageSdkPhase2Handlers
            schemaProof = new
            {
                keyTableCount = KeyTables.Length,
                confirmedTableCount = confirmed.Count,
                allKeyTablesPresent = allPresent,
                tables = schemaStatus,
                error = schemaError
            },
            capabilities,
            finding = allPresent
                ? $"All {KeyTables.Length} key Sage tables confirmed — connector fully operational."
                : $"{confirmed.Count}/{KeyTables.Length} key tables present — some capabilities may be limited.",
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static string? ParseConnectionCatalog(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(b.InitialCatalog) ? null : b.InitialCatalog;
        }
        catch { return null; }
    }
}
