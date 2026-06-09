using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

/// <summary>
/// CRUD and practice-mode lookup for FirmRecord.
/// Phase 4 Block 2 — multi-site accounting firm context.
/// </summary>
public sealed class FirmService(AppDbContext db)
{
    public async Task<List<FirmDto>> ListAsync(CancellationToken ct) =>
        await db.Firms.AsNoTracking()
            .OrderBy(f => f.Name)
            .Select(f => Map(f))
            .ToListAsync(ct);

    public async Task<FirmDto?> GetAsync(string firmId, CancellationToken ct) =>
        await db.Firms.AsNoTracking()
            .Where(f => f.FirmId == firmId)
            .Select(f => Map(f))
            .FirstOrDefaultAsync(ct);

    public async Task<FirmDto> CreateAsync(CreateFirmRequest request, CancellationToken ct)
    {
        var firm = new FirmRecord
        {
            FirmId = Guid.NewGuid().ToString(),
            Name = request.Name.Trim(),
            IsPracticeMode = request.IsPracticeMode,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Firms.Add(firm);
        await db.SaveChangesAsync(ct);
        return Map(firm);
    }

    public async Task<FirmDto?> UpdateAsync(string firmId, UpdateFirmRequest request, CancellationToken ct)
    {
        var firm = await db.Firms.FindAsync([firmId], ct);
        if (firm is null) return null;
        if (request.Name is not null) firm.Name = request.Name.Trim();
        if (request.IsPracticeMode.HasValue) firm.IsPracticeMode = request.IsPracticeMode.Value;
        await db.SaveChangesAsync(ct);
        return Map(firm);
    }

    public async Task<List<TenantDto>> ListTenantsAsync(string firmId, CancellationToken ct) =>
        await db.Tenants.AsNoTracking()
            .Where(t => t.FirmId == firmId)
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto { TenantId = t.TenantId, Name = t.Name })
            .ToListAsync(ct);

    public async Task<bool> AssignTenantAsync(string firmId, string tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([tenantId], ct);
        if (tenant is null) return false;
        tenant.FirmId = firmId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Looks up practice mode for the given tenant (via its firm).
    /// Returns false if the tenant has no firm or the firm is not in practice mode.
    /// </summary>
    public async Task<bool> IsPracticeModeAsync(string tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        if (tenant?.FirmId is null) return false;

        var firm = await db.Firms.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FirmId == tenant.FirmId, ct);
        return firm?.IsPracticeMode == true;
    }

    private static FirmDto Map(FirmRecord f) => new()
    {
        FirmId = f.FirmId,
        Name = f.Name,
        IsPracticeMode = f.IsPracticeMode,
        CreatedAtUtc = f.CreatedAtUtc
    };
}

public sealed class FirmDto
{
    public string FirmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPracticeMode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CreateFirmRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsPracticeMode { get; set; }
}

public sealed class UpdateFirmRequest
{
    public string? Name { get; set; }
    public bool? IsPracticeMode { get; set; }
}
