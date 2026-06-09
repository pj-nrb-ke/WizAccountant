using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace WizAccountant.Api;

/// <summary>
/// Phase 4 Block 4 (Task #19) — POPIA/GDPR compliance utilities.
///
/// Provides:
///   - Full tenant data export (Right of Access / Right to Data Portability)
///   - Tenant data erasure (Right to be Forgotten) — marks PII as redacted
///
/// All export operations are audit-logged. Accessible only to Admin+ roles
/// (enforced at the endpoint level via RbacMiddleware).
/// </summary>
public sealed class ComplianceService(AppDbContext db, ILogger<ComplianceService> logger)
{
    /// <summary>
    /// Exports all personal data held for a tenant.
    /// Returns a structured JSON payload suitable for POPIA/GDPR data-subject access requests.
    /// </summary>
    public async Task<ComplianceExportDto> ExportTenantDataAsync(string tenantId, CancellationToken ct)
    {
        logger.LogInformation("Compliance data export requested for tenant {TenantId}.", tenantId);

        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);

        if (tenant is null)
            throw new InvalidOperationException($"Tenant {tenantId} not found.");

        // Users
        var users = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Select(u => new ComplianceUserDto(u.UserId, u.Email, u.DisplayName, u.Role))
            .ToListAsync(ct);

        // Sites
        var sites = await db.Sites.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new ComplianceSiteDto(s.SiteId, s.SiteName, s.LastSeenUtc))
            .ToListAsync(ct);

        // Query log counts (no PII in counts)
        var queryLogCount = await db.InsightQueryLogs.AsNoTracking()
            .CountAsync(q => q.TenantId == tenantId, ct);

        // Conversation count
        var conversationCount = await db.ChatConversations.AsNoTracking()
            .CountAsync(c => c.TenantId == tenantId, ct);

        // Approval proposals
        var proposalCount = await db.ApprovalProposals.AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId, ct);

        // External identities
        var userIds = users.Select(u => u.UserId).ToHashSet();
        var externalIds = await db.ExternalIdentities.AsNoTracking()
            .Where(e => userIds.Contains(e.UserId))
            .Select(e => new ComplianceExternalIdDto(e.Provider, e.ProviderEmail, e.LinkedAtUtc))
            .ToListAsync(ct);

        var export = new ComplianceExportDto(
            ExportedAtUtc: DateTimeOffset.UtcNow,
            TenantId: tenantId,
            TenantName: tenant.Name,
            Users: users,
            Sites: sites,
            ExternalIdentities: externalIds,
            QueryLogCount: queryLogCount,
            ConversationCount: conversationCount,
            ApprovalProposalCount: proposalCount
        );

        logger.LogInformation(
            "Compliance export for tenant {TenantId}: {UserCount} users, {SiteCount} sites, {LogCount} query logs.",
            tenantId, users.Count, sites.Count, queryLogCount);

        return export;
    }

    /// <summary>
    /// Redacts PII for a specific user (Right to be Forgotten).
    /// Replaces email and display name with anonymised values, removes external identity links.
    /// </summary>
    public async Task RedactUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
                   ?? throw new InvalidOperationException($"User {userId} not found.");

        var redacted = $"redacted-{userId:N}@redacted.invalid";
        logger.LogInformation("Redacting PII for user {UserId} (was: {Email}).", userId, user.Email);

        user.Email = redacted;
        user.DisplayName = "[Redacted]";
        user.Password = string.Empty;

        // Remove all external identity links
        var links = await db.ExternalIdentities.Where(e => e.UserId == userId).ToListAsync(ct);
        db.ExternalIdentities.RemoveRange(links);

        await db.SaveChangesAsync(ct);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record ComplianceExportDto(
    DateTimeOffset ExportedAtUtc,
    string TenantId,
    string TenantName,
    List<ComplianceUserDto> Users,
    List<ComplianceSiteDto> Sites,
    List<ComplianceExternalIdDto> ExternalIdentities,
    int QueryLogCount,
    int ConversationCount,
    int ApprovalProposalCount
);

public sealed record ComplianceUserDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role
);

public sealed record ComplianceSiteDto(
    Guid SiteId,
    string SiteName,
    DateTimeOffset? LastSeenUtc
);

public sealed record ComplianceExternalIdDto(
    string Provider,
    string ProviderEmail,
    DateTimeOffset LinkedAtUtc
);
