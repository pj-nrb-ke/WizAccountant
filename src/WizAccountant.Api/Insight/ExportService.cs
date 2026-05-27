using System.Text;
using System.Text.Json;

namespace WizAccountant.Api.Insight;

public static class ExportService
{
    public static string? ToCsv(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return null;

        using var doc = JsonDocument.Parse(resultJson);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return null;

        var rows = items.EnumerateArray().ToList();
        if (rows.Count == 0) return "No rows";

        var headers = rows[0].EnumerateObject().Select(p => p.Name).ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));

        foreach (var row in rows)
        {
            var cells = headers.Select(h =>
            {
                if (!row.TryGetProperty(h, out var v)) return "";
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString() ?? "",
                    JsonValueKind.Number => v.GetRawText(),
                    _ => v.GetRawText()
                };
            });
            sb.AppendLine(string.Join(",", cells.Select(Escape)));
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        var v = value ?? "";
        if (v.Contains(',') || v.Contains('"'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
