using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

public sealed class JobService(
    AppDbContext db,
    IConnectorRegistry registry,
    IHubContext<ConnectorHub> hub,
    WizNotificationService notifications,
    ILogger<JobService> logger)
{
    public async Task<JobRecord> CreateAndDispatchAsync(CreateJobRequest request, CancellationToken ct)
    {
        var site = await db.Sites.FindAsync([request.SiteId], ct);
        if (site is null) throw new InvalidOperationException("Site not found.");

        var job = new JobRecord
        {
            JobId = Guid.NewGuid(),
            SiteId = request.SiteId,
            Operation = request.Operation,
            ParametersJson = JsonSerializer.Serialize(request.Parameters),
            Status = JobStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            RequestedBy = request.RequestedBy,
            IdempotencyKey = request.IdempotencyKey
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        await WriteAuditAsync(job, "Submitted", request.RequestedBy, null, ct);

        if (registry.TryGetConnectionId(request.SiteId, out var connectionId) && !string.IsNullOrWhiteSpace(connectionId))
        {
            job.Status = JobStatus.Running;
            job.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            await hub.Clients.Client(connectionId).SendAsync("RunJob", ToRunJobMessage(job, request.Parameters), ct);
            await WriteAuditAsync(job, "Dispatched", request.RequestedBy, "signalr", ct);
        }
        else
        {
            logger.LogWarning("Site {SiteId} is not connected; job {JobId} left pending for REST poll.", request.SiteId, job.JobId);
        }

        return job;
    }

    public async Task<JobDto> WaitForJobAsync(Guid jobId, int timeoutSeconds, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(timeoutSeconds, 5, 120));
        while (DateTimeOffset.UtcNow < deadline)
        {
            var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == jobId, ct);
            if (job is null) throw new InvalidOperationException("Job not found.");

            if (job.Status is JobStatus.Completed or JobStatus.Failed)
                return ToJobDto(job);

            await Task.Delay(500, ct);
        }

        var pending = await db.Jobs.FindAsync([jobId], ct);
        if (pending is not null)
            await WriteAuditAsync(pending, "TimedOut", pending.RequestedBy, "Job did not complete before timeout.", ct);

        throw new TimeoutException("The connector did not respond in time. Use “Start programs on this PC” and try again.");
    }

    public async Task<JobDto> RunAndWaitAsync(CreateJobRequest request, int timeoutSeconds, CancellationToken ct)
    {
        var job = await CreateAndDispatchAsync(request, ct);
        return await WaitForJobAsync(job.JobId, timeoutSeconds, ct);
    }

    public async Task RecordResultAsync(Guid jobId, SubmitJobResultRequest result, CancellationToken ct)
    {
        var job = await db.Jobs.FindAsync([jobId], ct);
        if (job is null) return;

        job.Status = string.IsNullOrWhiteSpace(result.Error) ? JobStatus.Completed : JobStatus.Failed;
        job.ResultJson = result.ResultJson;
        job.Error = result.Error;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await WriteAuditAsync(
            job,
            job.Status == JobStatus.Completed ? "Completed" : "Failed",
            job.RequestedBy,
            result.Error,
            ct);

        // B5-B: push real-time notification to UI
        var site = await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.SiteId == job.SiteId, ct);
        if (site is not null)
        {
            await notifications.PushJobCompletedAsync(
                site.TenantId, job.SiteId, job.JobId,
                job.Status == JobStatus.Completed, ct);
        }
    }

    /// <summary>P1-26: long-poll next pending job for a paired connector (REST fallback).</summary>
    public async Task<ConnectorJobPollResponse?> PollNextJobAsync(
        Guid siteId,
        string deviceId,
        int waitSeconds,
        CancellationToken ct)
    {
        var site = await db.Sites.FindAsync([siteId], ct);
        if (site is null || !string.Equals(site.DeviceId, deviceId.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(waitSeconds, 5, 120));
        while (DateTimeOffset.UtcNow < deadline)
        {
            var job = await db.Jobs
                .Where(j => j.SiteId == siteId && j.Status == JobStatus.Pending)
                .OrderBy(j => j.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (job is not null)
            {
                job.Status = JobStatus.Running;
                job.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await WriteAuditAsync(job, "Dispatched", job.RequestedBy, "rest-poll", ct);

                var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(job.ParametersJson)
                                 ?? new Dictionary<string, string>();
                return new ConnectorJobPollResponse { HasJob = true, Job = ToRunJobMessage(job, parameters) };
            }

            await Task.Delay(1000, ct);
        }

        return new ConnectorJobPollResponse { HasJob = false };
    }

    public async Task<List<JobAuditDto>> ListAuditAsync(int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 500);
        var rows = await db.JobAudits.ToListAsync(ct);
        rows = rows.OrderByDescending(a => a.TimestampUtc).Take(take).ToList();

        var siteNames = await db.Sites.AsNoTracking().ToDictionaryAsync(s => s.SiteId, s => s.SiteName, ct);

        return rows.Select(a => new JobAuditDto
        {
            AuditId = a.AuditId,
            JobId = a.JobId,
            SiteId = a.SiteId,
            SiteName = siteNames.TryGetValue(a.SiteId, out var name) ? name : "",
            Operation = a.Operation,
            EventType = a.EventType,
            RequestedBy = a.RequestedBy,
            Success = a.Success,
            Detail = a.Detail,
            TimestampUtc = a.TimestampUtc
        }).ToList();
    }

    private async Task WriteAuditAsync(JobRecord job, string eventType, string? requestedBy, string? detail, CancellationToken ct)
    {
        db.JobAudits.Add(new JobAuditRecord
        {
            AuditId = Guid.NewGuid(),
            JobId = job.JobId,
            SiteId = job.SiteId,
            Operation = job.Operation,
            EventType = eventType,
            RequestedBy = requestedBy,
            Success = eventType is "Completed" ? true : eventType is "Failed" or "TimedOut" ? false : null,
            Detail = detail,
            TimestampUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static RunJobMessage ToRunJobMessage(JobRecord job, Dictionary<string, string> parameters) => new()
    {
        JobId = job.JobId,
        SiteId = job.SiteId,
        Operation = job.Operation,
        Parameters = parameters,
        IdempotencyKey = job.IdempotencyKey
    };

    public static JobDto ToJobDto(JobRecord x) => new()
    {
        JobId = x.JobId,
        SiteId = x.SiteId,
        Operation = x.Operation,
        Status = x.Status,
        ResultJson = x.ResultJson,
        Error = x.Error,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc
    };
}
