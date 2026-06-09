using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api.Act;

public static class ProposalTypeMap
{
    public static string ToOperation(string proposalType) => proposalType.ToLowerInvariant() switch
    {
        "gl.journal" => "gltransaction.post",
        "ar.transaction" => "customertransaction.post",
        "ap.transaction" => "suppliertransaction.post",
        "ar.allocation" => "allocation.save",
        "customer.master" => "customer.save",
        "supplier.master" => "supplier.save",
        // Phase 4 Block 3 — inventory + credit note + order lifecycle
        "inventory.adjustment" => "inventory.adjustment.post",
        "warehouse.transfer" => "warehouse.transfer.post",
        "sales.creditnote" => "salescreditnote.post",
        "supplier.creditnote" => "suppliercreditnote.post",
        "salesorder.confirm" => "salesorder.confirm",
        "salesorder.ship" => "salesorder.ship",
        "purchaseorder.approve" => "purchaseorder.approve",
        "purchaseorder.receive" => "purchaseorder.receive",
        _ => throw new InvalidOperationException($"Unknown proposal type: {proposalType}")
    };
}

public sealed class ApprovalService(AppDbContext db, JobService jobs, WizNotificationService notifications, ILogger<ApprovalService> logger)
{
    public async Task<ApprovalProposalDto> ProposeAsync(ProposeApprovalRequest request, CancellationToken ct)
    {
        var site = await db.Sites.FindAsync([request.SiteId], ct)
                   ?? throw new InvalidOperationException("Site not found.");
        var user = await db.Users.FindAsync([request.PreparedByUserId], ct)
                   ?? throw new InvalidOperationException("User not found.");

        if (user.Role is not ("Preparer" or "Admin"))
            throw new InvalidOperationException("Only Preparer or Admin can create proposals.");

        // Practice mode guard (GAP — Phase 4 Block 2): block writes for firms in practice mode
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == site.TenantId, ct);
        if (tenant?.FirmId is not null)
        {
            var firm = await db.Firms.AsNoTracking()
                .FirstOrDefaultAsync(f => f.FirmId == tenant.FirmId, ct);
            if (firm?.IsPracticeMode == true)
                throw new InvalidOperationException(
                    "This firm is in practice mode — write proposals are not allowed. " +
                    "Contact your FirmAdmin to disable practice mode.");
        }

        var proposal = new ApprovalProposalRecord
        {
            ProposalId = Guid.NewGuid(),
            SiteId = request.SiteId,
            TenantId = site.TenantId,
            ProposalType = request.ProposalType,
            Title = request.Title,
            PayloadJson = request.PayloadJson,
            Status = ApprovalStatus.Pending,
            PreparedByUserId = request.PreparedByUserId,
            IdempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString("N"),
            Comment = request.Comment,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.ApprovalProposals.Add(proposal);
        await db.SaveChangesAsync(ct);

        // B5-B: notify approvers in real-time
        await notifications.PushApprovalRequiredAsync(site.TenantId, proposal.ProposalId, proposal.Title, ct);

        return await ToDtoAsync(proposal, ct);
    }

    public async Task<List<ApprovalProposalDto>> ListAsync(Guid? siteId, ApprovalStatus? status, CancellationToken ct)
    {
        var q = db.ApprovalProposals.AsNoTracking();
        if (siteId is not null) q = q.Where(p => p.SiteId == siteId);
        if (status is not null) q = q.Where(p => p.Status == status);
        var rows = (await q.ToListAsync(ct))
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(100)
            .ToList();
        var result = new List<ApprovalProposalDto>();
        foreach (var row in rows)
            result.Add(await ToDtoAsync(row, ct));
        return result;
    }

    public async Task<ApprovalProposalDto> ApproveAsync(Guid proposalId, ApproveProposalRequest request, CancellationToken ct)
    {
        var proposal = await db.ApprovalProposals.FindAsync([proposalId], ct)
                       ?? throw new InvalidOperationException("Proposal not found.");
        if (proposal.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("Proposal is not pending.");

        var approver = await db.Users.FindAsync([request.ApproverUserId], ct)
                       ?? throw new InvalidOperationException("Approver not found.");
        if (approver.Role is not ("Approver" or "Admin"))
            throw new InvalidOperationException("Only Approver or Admin can approve.");

        if (approver.Role != "Admin" && proposal.PreparedByUserId == request.ApproverUserId)
            throw new InvalidOperationException("Maker-checker: approver must differ from preparer.");

        var operation = ProposalTypeMap.ToOperation(proposal.ProposalType);
        var beforeJson = proposal.PayloadJson;

        JobDto jobResult;
        try
        {
            jobResult = await jobs.RunAndWaitAsync(new CreateJobRequest
            {
                SiteId = proposal.SiteId,
                Operation = operation,
                Parameters = new Dictionary<string, string>
                {
                    ["payload"] = proposal.PayloadJson,
                    ["idempotencyKey"] = proposal.IdempotencyKey ?? proposal.ProposalId.ToString("N")
                },
                RequestedBy = $"approver:{request.ApproverUserId}",
                IdempotencyKey = proposal.IdempotencyKey
            }, 120, ct);
        }
        catch (Exception ex)
        {
            proposal.Status = ApprovalStatus.Failed;
            proposal.ResolvedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogError(ex, "Approval post failed for {ProposalId}", proposalId);
            throw;
        }

        proposal.ApprovedByUserId = request.ApproverUserId;
        proposal.JobId = jobResult.JobId;
        proposal.ResolvedAtUtc = DateTimeOffset.UtcNow;
        proposal.Status = jobResult.Status == JobStatus.Completed ? ApprovalStatus.Posted : ApprovalStatus.Failed;

        var evolutionRef = TryExtractRef(jobResult.ResultJson);
        db.WriteAudits.Add(new WriteAuditRecord
        {
            WriteAuditId = Guid.NewGuid(),
            ProposalId = proposal.ProposalId,
            SiteId = proposal.SiteId,
            Operation = operation,
            BeforeJson = beforeJson,
            AfterJson = jobResult.ResultJson,
            PreparerUserId = proposal.PreparedByUserId,
            ApproverUserId = request.ApproverUserId,
            EvolutionRef = evolutionRef,
            Success = proposal.Status == ApprovalStatus.Posted,
            TimestampUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(proposal, ct);
    }

    public async Task<ApprovalProposalDto> RejectAsync(Guid proposalId, RejectProposalRequest request, CancellationToken ct)
    {
        var proposal = await db.ApprovalProposals.FindAsync([proposalId], ct)
                       ?? throw new InvalidOperationException("Proposal not found.");
        if (proposal.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException("Proposal is not pending.");

        var approver = await db.Users.FindAsync([request.ApproverUserId], ct)
                       ?? throw new InvalidOperationException("Approver not found.");
        if (approver.Role is not ("Approver" or "Admin"))
            throw new InvalidOperationException("Only Approver or Admin can reject.");

        proposal.Status = ApprovalStatus.Rejected;
        proposal.ApprovedByUserId = request.ApproverUserId;
        proposal.RejectReason = request.Reason;
        proposal.ResolvedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(proposal, ct);
    }

    public async Task<List<WriteAuditDto>> ListWriteAuditAsync(Guid? siteId, CancellationToken ct)
    {
        var q = db.WriteAudits.AsNoTracking();
        if (siteId is not null) q = q.Where(a => a.SiteId == siteId);
        var auditRows = (await q.ToListAsync(ct))
            .OrderByDescending(a => a.TimestampUtc)
            .Take(100);
        return auditRows
            .Select(a => new WriteAuditDto
            {
                WriteAuditId = a.WriteAuditId,
                ProposalId = a.ProposalId,
                SiteId = a.SiteId,
                Operation = a.Operation,
                BeforeJson = a.BeforeJson,
                AfterJson = a.AfterJson,
                PreparerUserId = a.PreparerUserId,
                ApproverUserId = a.ApproverUserId,
                EvolutionRef = a.EvolutionRef,
                Success = a.Success,
                TimestampUtc = a.TimestampUtc
            })
            .ToList();
    }

    private async Task<ApprovalProposalDto> ToDtoAsync(ApprovalProposalRecord p, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == p.SiteId, ct);
        var preparer = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == p.PreparedByUserId, ct);
        var approver = p.ApprovedByUserId is { } aid
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == aid, ct)
            : null;

        return new ApprovalProposalDto
        {
            ProposalId = p.ProposalId,
            SiteId = p.SiteId,
            SiteName = site?.SiteName ?? "",
            ProposalType = p.ProposalType,
            Title = p.Title,
            PayloadJson = p.PayloadJson,
            Status = p.Status,
            PreparedByUserId = p.PreparedByUserId,
            PreparedByName = preparer?.DisplayName ?? "",
            ApprovedByUserId = p.ApprovedByUserId,
            ApprovedByName = approver?.DisplayName,
            IdempotencyKey = p.IdempotencyKey,
            JobId = p.JobId,
            Comment = p.Comment,
            RejectReason = p.RejectReason,
            CreatedAtUtc = p.CreatedAtUtc,
            ResolvedAtUtc = p.ResolvedAtUtc
        };
    }

    private static string? TryExtractRef(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            if (doc.RootElement.TryGetProperty("evolutionRef", out var r)) return r.GetString();
            if (doc.RootElement.TryGetProperty("reference", out var r2)) return r2.GetString();
        }
        catch { /* ignore */ }
        return null;
    }
}
