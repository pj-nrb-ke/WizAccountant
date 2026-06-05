using System.Text.Json;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

public sealed class InsightSqlQueryService(JobService jobs)
{
    public async Task<InsightSqlQueryResponse> RunAsync(InsightSqlQueryRequest request, CancellationToken ct)
    {
        if (request.SiteId == Guid.Empty)
            throw new ArgumentException("SiteId is required.");
        if (!InsightSqlGuard.IsReadOnlySelect(request.Sql, out var reason))
            throw new InvalidOperationException(reason ?? "SQL query rejected.");
        if (!InsightReadOnlyTools.IsAllowed("insight.sql.query"))
            throw new InvalidOperationException("SQL query operation is not allowlisted.");

        var maxRows = Math.Clamp(request.MaxRows ?? 500, 1, 2000);
        var job = await jobs.RunAndWaitAsync(new CreateJobRequest
        {
            SiteId = request.SiteId,
            Operation = "insight.sql.query",
            Parameters = new Dictionary<string, string>
            {
                ["sql"] = request.Sql.Trim(),
                ["maxRows"] = maxRows.ToString()
            },
            RequestedBy = "insight-sql-tab"
        }, 120, ct);

        if (job.Status == JobStatus.Failed)
            throw new InvalidOperationException(SafeExecutionBoundary.SanitizeForUser(job.Error ?? "SQL query failed."));

        var grid = BuildGrid(job.ResultJson);
        var meta = ParseMeta(job.ResultJson);
        return new InsightSqlQueryResponse
        {
            Grid = grid,
            RowCount = meta.RowCount,
            Truncated = meta.Truncated,
            Message = meta.Truncated
                ? $"Showing first {meta.RowCount} row(s) (limit {meta.MaxRows}). Narrow your query for more."
                : $"{meta.RowCount} row(s) returned in {meta.ElapsedMs} ms.",
            DataAsOfUtc = job.UpdatedAtUtc ?? job.CreatedAtUtc,
            JobId = job.JobId
        };
    }

    public async Task<InvoiceLineSqlHintResponse> GetInvoiceLineHintAsync(Guid siteId, CancellationToken ct)
    {
        if (siteId == Guid.Empty)
            throw new ArgumentException("SiteId is required.");
        if (!InsightReadOnlyTools.IsAllowed("insight.sql.invoice-lines-hint"))
            throw new InvalidOperationException("Invoice line schema hint is not allowlisted.");

        var job = await jobs.RunAndWaitAsync(new CreateJobRequest
        {
            SiteId = siteId,
            Operation = "insight.sql.invoice-lines-hint",
            Parameters = new Dictionary<string, string>(),
            RequestedBy = "insight-sql-tab"
        }, 60, ct);

        if (job.Status == JobStatus.Failed)
            throw new InvalidOperationException(SafeExecutionBoundary.SanitizeForUser(job.Error ?? "Schema hint failed."));

        return ParseInvoiceLineHint(job.ResultJson);
    }

    internal static InvoiceLineSqlHintResponse ParseInvoiceLineHint(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return new InvoiceLineSqlHintResponse();

        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        var hint = new InvoiceLineSqlHintResponse
        {
            TableName = root.TryGetProperty("tableName", out var tn) ? tn.GetString() ?? "_btblInvoiceLines" : "_btblInvoiceLines",
            QtyColumn = root.TryGetProperty("qtyColumn", out var qc) ? qc.GetString() ?? "" : "",
            QtyExpression = root.TryGetProperty("qtyExpression", out var qe) ? qe.GetString() ?? "" : "",
            ValueExpression = root.TryGetProperty("valueExpression", out var ve) ? ve.GetString() ?? "" : "",
            ValueSource = root.TryGetProperty("valueSource", out var vs) ? vs.GetString() ?? "" : "",
            SampleProductMonthlySql = root.TryGetProperty("sampleProductMonthlySql", out var ss) ? ss.GetString() : null
        };

        if (root.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cols.EnumerateArray())
            {
                var name = c.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    hint.Columns.Add(name);
            }
        }

        return hint;
    }

    internal static ChatGridDto BuildGrid(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return new ChatGridDto();

        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("columns", out var colsEl) || colsEl.ValueKind != JsonValueKind.Array)
            return new ChatGridDto();

        var columns = colsEl.EnumerateArray()
            .Select(c => c.GetString() ?? "")
            .Where(c => c.Length > 0)
            .ToList();

        var rows = new List<Dictionary<string, string?>>();
        if (root.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var rowEl in rowsEl.EnumerateArray())
            {
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in rowEl.EnumerateObject())
                    row[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
                rows.Add(row);
            }
        }

        if (columns.Count == 0 && rows.Count > 0)
            columns = rows[0].Keys.ToList();

        return new ChatGridDto { Columns = columns, Rows = rows };
    }

    private static (int RowCount, bool Truncated, int MaxRows, long ElapsedMs) ParseMeta(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
            return (0, false, 500, 0);
        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        var count = root.TryGetProperty("rowCount", out var rc) ? rc.GetInt32() : 0;
        var truncated = root.TryGetProperty("truncated", out var tr) && tr.GetBoolean();
        var max = root.TryGetProperty("maxRows", out var mr) ? mr.GetInt32() : 500;
        var ms = root.TryGetProperty("elapsedMs", out var em) ? em.GetInt64() : 0;
        return (count, truncated, max, ms);
    }
}
