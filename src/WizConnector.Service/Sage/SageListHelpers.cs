using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WizConnector.Service.Sage;

internal static class SageListHelpers
{
    public static string BuildCriteria(Dictionary<string, string> parameters, string defaultCriteria)
    {
        if (parameters.TryGetValue("criteria", out var explicitCriteria) && !string.IsNullOrWhiteSpace(explicitCriteria))
            return explicitCriteria;

        var parts = new List<string> { defaultCriteria };
        if (parameters.TryGetValue("account", out var account) && !string.IsNullOrWhiteSpace(account))
            parts.Add($"Account = '{EscapeLiteral(account)}'");

        if (parameters.TryGetValue("reference", out var reference) && !string.IsNullOrWhiteSpace(reference))
            parts.Add($"Reference LIKE '%{EscapeLiteral(reference)}%'");

        if (parameters.TryGetValue("dateFrom", out var dateFrom) && !string.IsNullOrWhiteSpace(dateFrom))
            parts.Add($"TxDate >= '{EscapeLiteral(dateFrom)}'");

        if (parameters.TryGetValue("dateTo", out var dateTo) && !string.IsNullOrWhiteSpace(dateTo))
            parts.Add($"TxDate <= '{EscapeLiteral(dateTo)}'");

        return string.Join(" AND ", parts.Distinct());
    }

    public static string SerializePaged<T>(List<T> allItems, string criteria, Dictionary<string, string> parameters)
    {
        var skip = ParseInt(parameters, "skip", 0);
        var top = Math.Clamp(ParseInt(parameters, "top", 100), 1, 500);
        var page = allItems.Skip(skip).Take(top).ToList();

        return JsonSerializer.Serialize(new
        {
            items = page,
            criteria,
            total = allItems.Count,
            skip,
            top,
            dataAsOfUtc = DateTimeOffset.UtcNow
        });
    }

    public static List<T> MapRows<T>(DataTable? table, Func<DataRow, T> map)
    {
        var list = new List<T>();
        if (table is null) return list;
        foreach (DataRow row in table.Rows)
            list.Add(map(row));
        return list;
    }

    public static string? Col(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.Table.Columns.Contains(name)) continue;
            return row[name]?.ToString();
        }
        return null;
    }

    public static string SanitizeSearchQuery(string query)
    {
        var cleaned = Regex.Replace(query.Trim(), @"[^a-zA-Z0-9\s\.\-_]", "");
        return cleaned.Length > 64 ? cleaned[..64] : cleaned;
    }

    private static int ParseInt(Dictionary<string, string> parameters, string key, int defaultValue) =>
        parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? Math.Max(0, value) : defaultValue;

    private static string EscapeLiteral(string value) => value.Replace("'", "''");
}
