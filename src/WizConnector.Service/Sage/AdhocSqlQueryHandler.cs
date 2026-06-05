using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>Read-only ad-hoc SQL for Insight SQL tab (company database).</summary>
internal static class AdhocSqlQueryHandler
{
    public const string QuerySerial = "INSIGHT-ADHOC-SQL-001";
    public const string Operation = "insight.sql.query";
    private const int DefaultMaxRows = 500;
    private const int AbsoluteMaxRows = 2000;

    public static string Execute(string companyConnectionString, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException("Sage company database connection is not configured.");

        if (!parameters.TryGetValue("sql", out var sql) || string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("Parameter 'sql' is required.");

        if (!InsightSqlGuard.IsReadOnlySelect(sql, out var reason))
            throw new InvalidOperationException(reason ?? "SQL query rejected.");

        var maxRows = ParseMaxRows(parameters);
        var sw = Stopwatch.StartNew();
        var columns = new List<string>();
        var rows = new List<Dictionary<string, string?>>();
        var truncated = false;

        using (var conn = new SqlConnection(companyConnectionString))
        {
            conn.Open();
            using var cmd = new SqlCommand(sql.Trim(), conn) { CommandTimeout = 180 };
            using var reader = cmd.ExecuteReader();
            for (var i = 0; i < reader.FieldCount; i++)
                columns.Add(reader.GetName(i));

            while (reader.Read())
            {
                if (rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }

                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    row[name] = reader.IsDBNull(i) ? null : FormatCell(reader.GetValue(i));
                }
                rows.Add(row);
            }
        }

        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            querySerial = QuerySerial,
            operation = Operation,
            columns,
            rows,
            rowCount = rows.Count,
            truncated,
            maxRows,
            elapsedMs = sw.ElapsedMilliseconds,
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    private static int ParseMaxRows(Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("maxRows", out var raw) && int.TryParse(raw, out var n))
            return Math.Clamp(n, 1, AbsoluteMaxRows);
        return DefaultMaxRows;
    }

    private static string FormatCell(object value) => value switch
    {
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        decimal d => d.ToString("G29", CultureInfo.InvariantCulture),
        double dbl => dbl.ToString("G17", CultureInfo.InvariantCulture),
        float f => f.ToString("G9", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
}
