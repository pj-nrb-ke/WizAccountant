using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Insight;

public sealed class InsightSavedSqlQueryService(AppDbContext db)
{
    public async Task<IReadOnlyList<InsightSavedSqlQueryDto>> ListAsync(
        string tenantId,
        Guid siteId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.");
        if (siteId == Guid.Empty)
            throw new ArgumentException("SiteId is required.");

        return await db.InsightSavedSqlQueries
            .AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.SiteId == siteId)
            .OrderByDescending(q => q.UpdatedAtUtc)
            .Select(q => new InsightSavedSqlQueryDto
            {
                QueryId = q.QueryId,
                SiteId = q.SiteId,
                Title = q.Title,
                AiPrompt = q.AiPrompt,
                Sql = q.Sql,
                CreatedAtUtc = q.CreatedAtUtc,
                UpdatedAtUtc = q.UpdatedAtUtc
            })
            .ToListAsync(ct);
    }

    public async Task<InsightSavedSqlQueryDto> UpsertAsync(
        string tenantId,
        UpsertInsightSavedSqlQueryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.");
        if (request.SiteId == Guid.Empty)
            throw new ArgumentException("SiteId is required.");

        var title = request.Title?.Trim() ?? "";
        var sql = request.Sql?.Trim() ?? "";
        if (title.Length == 0)
            throw new InvalidOperationException("Title is required.");
        if (sql.Length == 0)
            throw new InvalidOperationException("SQL is required.");

        var site = await db.Sites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SiteId == request.SiteId, ct);
        if (site is null || !string.Equals(site.TenantId, tenantId, StringComparison.Ordinal))
            throw new InvalidOperationException("Site not found for this tenant.");

        var now = DateTimeOffset.UtcNow.ToString("O");
        InsightSavedSqlQueryRecord row;

        if (request.QueryId is Guid id && id != Guid.Empty)
        {
            row = await db.InsightSavedSqlQueries
                .FirstOrDefaultAsync(q => q.QueryId == id && q.TenantId == tenantId && q.SiteId == request.SiteId, ct)
                ?? throw new InvalidOperationException("Saved query not found.");
            row.Title = Truncate(title, 200);
            row.AiPrompt = string.IsNullOrWhiteSpace(request.AiPrompt) ? null : Truncate(request.AiPrompt.Trim(), 4000);
            row.Sql = Truncate(sql, 50000);
            row.UpdatedAtUtc = now;
        }
        else
        {
            row = new InsightSavedSqlQueryRecord
            {
                QueryId = Guid.NewGuid(),
                TenantId = tenantId,
                SiteId = request.SiteId,
                Title = Truncate(title, 200),
                AiPrompt = string.IsNullOrWhiteSpace(request.AiPrompt) ? null : Truncate(request.AiPrompt.Trim(), 4000),
                Sql = Truncate(sql, 50000),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.InsightSavedSqlQueries.Add(row);
        }

        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<bool> DeleteAsync(string tenantId, Guid siteId, Guid queryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.");
        if (siteId == Guid.Empty || queryId == Guid.Empty)
            throw new ArgumentException("SiteId and QueryId are required.");

        var row = await db.InsightSavedSqlQueries
            .FirstOrDefaultAsync(q => q.QueryId == queryId && q.TenantId == tenantId && q.SiteId == siteId, ct);
        if (row is null)
            return false;

        db.InsightSavedSqlQueries.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static InsightSavedSqlQueryDto ToDto(InsightSavedSqlQueryRecord q) => new()
    {
        QueryId = q.QueryId,
        SiteId = q.SiteId,
        Title = q.Title,
        AiPrompt = q.AiPrompt,
        Sql = q.Sql,
        CreatedAtUtc = q.CreatedAtUtc,
        UpdatedAtUtc = q.UpdatedAtUtc
    };

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];
}
