using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WizAccountant.Api.Insight;

public static class ExportService
{
    // ── CSV ──────────────────────────────────────────────────────────────────

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

    // ── Excel (ClosedXML) ────────────────────────────────────────────────────

    public static byte[]? ToExcel(string? resultJson, string sheetTitle = "Export")
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return null;

        using var doc = JsonDocument.Parse(resultJson);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return null;

        var rows = items.EnumerateArray().ToList();
        if (rows.Count == 0) return null;

        var headers = rows[0].EnumerateObject().Select(p => p.Name).ToList();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetTitle.Length > 31 ? sheetTitle[..31] : sheetTitle);

        // Header row — bold
        for (var c = 0; c < headers.Count; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        // Data rows
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < headers.Count; c++)
            {
                if (!row.TryGetProperty(headers[c], out var v)) continue;
                ws.Cell(r + 2, c + 1).Value = v.ValueKind switch
                {
                    JsonValueKind.Number => v.TryGetDouble(out var d)
                        ? XLCellValue.FromObject(d)
                        : XLCellValue.FromObject(v.GetRawText()),
                    JsonValueKind.True  => XLCellValue.FromObject(true),
                    JsonValueKind.False => XLCellValue.FromObject(false),
                    JsonValueKind.Null  => XLCellValue.FromObject(string.Empty),
                    _                  => XLCellValue.FromObject(v.GetString() ?? v.GetRawText()),
                };
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── PDF (QuestPDF) ───────────────────────────────────────────────────────

    public static byte[]? ToPdf(string? resultJson, string reportTitle = "WizAccountant Export")
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return null;

        using var doc = JsonDocument.Parse(resultJson);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return null;

        var rows = items.EnumerateArray().ToList();
        if (rows.Count == 0) return null;

        var headers = rows[0].EnumerateObject().Select(p => p.Name).ToList();

        // Use Community licence (free for non-commercial; production set to Professional via env var)
        QuestPDF.Settings.License = LicenseType.Community;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4.Landscape());

                page.Header()
                    .Text(reportTitle)
                    .Bold().FontSize(14).AlignCenter();

                page.Content()
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in headers)
                                columns.RelativeColumn();
                        });

                        // Header
                        table.Header(header =>
                        {
                            foreach (var h in headers)
                                header.Cell()
                                    .Background(Colors.Grey.Lighten2)
                                    .Padding(4)
                                    .Text(h).Bold().FontSize(8);
                        });

                        // Rows
                        foreach (var row in rows)
                        {
                            foreach (var h in headers)
                            {
                                var val = row.TryGetProperty(h, out var v)
                                    ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()) ?? ""
                                    : "";
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
                                    .Padding(3).Text(val).FontSize(7);
                            }
                        }
                    });

                page.Footer()
                    .AlignRight()
                    .Text(t =>
                    {
                        t.Span("Page ").FontSize(8);
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" of ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
            });
        });

        return pdf.GeneratePdf();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Escape(string? value)
    {
        var v = value ?? "";
        if (v.Contains(',') || v.Contains('"'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
