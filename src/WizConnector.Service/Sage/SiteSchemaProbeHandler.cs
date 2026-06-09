using System.Text.Json;

namespace WizConnector.Service.Sage;

/// <summary>
/// Probes the Sage company database schema via INFORMATION_SCHEMA — returns available
/// tables and column names for known Evolution core tables. Closes GAP-020/GAP-021.
/// </summary>
internal static class SiteSchemaProbeHandler
{
    /// <summary>Default set of Evolution core tables to probe.</summary>
    private static readonly string[] DefaultTables =
    [
        "Client", "Vendor", "PostAR", "PostAP", "PostGL", "InvNum",
        "Accounts", "StkItem", "WhseStock", "StkMovement",
        "_etblGLAccountTypes", "GrpTbl", "_btblInvoiceLines"
    ];

    public static string Execute(string connectionString, Dictionary<string, string> parameters)
    {
        var tablesToProbe = parameters.TryGetValue("tables", out var t) && !string.IsNullOrWhiteSpace(t)
            ? t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : DefaultTables;

        // Single batched INFORMATION_SCHEMA query — one round-trip for all tables
        var paramNames = Enumerable.Range(0, tablesToProbe.Length).Select(i => $"@t{i}").ToArray();
        var sql = $"""
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME IN ({string.Join(",", paramNames)})
            ORDER BY TABLE_NAME, ORDINAL_POSITION
            """;

        var rows = GlSqlHelper.ExecuteQuery(connectionString, sql, cmd =>
        {
            for (var i = 0; i < tablesToProbe.Length; i++)
                cmd.Parameters.AddWithValue(paramNames[i], tablesToProbe[i]);
        });

        // Group by table name
        var byTable = rows
            .GroupBy(r => r["TABLE_NAME"]?.ToString() ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r["COLUMN_NAME"]?.ToString() ?? "")
                       .Where(s => s.Length > 0)
                       .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var results = tablesToProbe.Select(t2 =>
        {
            var cols = byTable.TryGetValue(t2, out var c) ? c : [];
            return new
            {
                tableName = t2,
                exists = cols.Count > 0,
                columnCount = cols.Count,
                columns = cols
            };
        }).ToList();

        var present = results.Count(r => r.exists);
        var missing = results.Count(r => !r.exists);

        return JsonSerializer.Serialize(new
        {
            tableCount = tablesToProbe.Length,
            tablesPresent = present,
            tablesMissing = missing,
            tables = results,
            missingTables = results.Where(r => !r.exists).Select(r => r.tableName).ToList(),
            finding = $"Schema probe: {present}/{tablesToProbe.Length} tables confirmed on this Sage database."
                      + (missing > 0 ? $" Missing: {string.Join(", ", results.Where(r => !r.exists).Select(r => r.tableName))}." : ""),
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }
}
