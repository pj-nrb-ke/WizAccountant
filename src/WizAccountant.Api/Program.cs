using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Api;
using WizAccountant.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "WizAccountant.Api" }));

app.MapPost("/api/pairing-codes", async (CreatePairingCodeRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.SiteName))
    {
        return Results.BadRequest("tenantId and siteName are required.");
    }

    var code = $"WZ{Random.Shared.Next(100000, 999999)}";
    var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(request.ExpiresInMinutes, 5, 60));
    db.PairingCodes.Add(new PairingCodeRecord
    {
        PairingCodeId = Guid.NewGuid(),
        Code = code,
        TenantId = request.TenantId.Trim(),
        SiteName = request.SiteName.Trim(),
        ExpiresAtUtc = expires
    });
    await db.SaveChangesAsync();
    return Results.Ok(new PairingCodeResponse { PairingCode = code, ExpiresAtUtc = expires });
});

app.MapPost("/api/sites/pair", async (PairSiteRequest request, HttpContext http, AppDbContext db) =>
{
    var candidates = await db.PairingCodes
        .Where(p => p.Code == request.PairingCode && !p.IsUsed)
        .ToListAsync();
    var pairing = candidates.FirstOrDefault(p => p.ExpiresAtUtc > DateTimeOffset.UtcNow);
    if (pairing is null) return Results.BadRequest("Invalid or expired pairing code.");

    var site = new SiteRecord
    {
        SiteId = Guid.NewGuid(),
        TenantId = pairing.TenantId,
        SiteName = pairing.SiteName,
        DeviceId = request.DeviceId.Trim(),
        IsOnline = false
    };

    pairing.IsUsed = true;
    db.Sites.Add(site);
    await db.SaveChangesAsync();

    return Results.Ok(new PairSiteResponse
    {
        SiteId = site.SiteId,
        TenantId = site.TenantId,
        SiteName = site.SiteName,
        HubPath = "/hubs/connector"
    });
});

app.MapGet("/api/sites", async (AppDbContext db) =>
{
    var sites = await db.Sites.OrderBy(x => x.SiteName)
        .Select(s => new SiteDto
        {
            SiteId = s.SiteId,
            TenantId = s.TenantId,
            SiteName = s.SiteName,
            DeviceId = s.DeviceId,
            IsOnline = s.IsOnline,
            LastSeenUtc = s.LastSeenUtc
        }).ToListAsync();
    return Results.Ok(sites);
});

app.MapPost("/api/jobs", async (CreateJobRequest request, AppDbContext db, IConnectorRegistry registry, IHubContext<ConnectorHub> hub) =>
{
    var site = await db.Sites.FindAsync(request.SiteId);
    if (site is null) return Results.NotFound("Site not found.");

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
    await db.SaveChangesAsync();

    if (registry.TryGetConnectionId(request.SiteId, out var connectionId) && !string.IsNullOrWhiteSpace(connectionId))
    {
        await hub.Clients.Client(connectionId).SendAsync("RunJob", new RunJobMessage
        {
            JobId = job.JobId,
            SiteId = job.SiteId,
            Operation = job.Operation,
            Parameters = request.Parameters,
            IdempotencyKey = request.IdempotencyKey
        });
    }

    return Results.Ok(ToJobDto(job));
});

app.MapPost("/api/jobs/{jobId:guid}/result", async (Guid jobId, SubmitJobResultRequest result, AppDbContext db) =>
{
    var job = await db.Jobs.FindAsync(jobId);
    if (job is null) return Results.NotFound();

    job.Status = string.IsNullOrWhiteSpace(result.Error) ? JobStatus.Completed : JobStatus.Failed;
    job.ResultJson = result.ResultJson;
    job.Error = result.Error;
    job.UpdatedAtUtc = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(ToJobDto(job));
});

app.MapGet("/api/jobs/{jobId:guid}", async (Guid jobId, AppDbContext db) =>
{
    var job = await db.Jobs.FindAsync(jobId);
    return job is null ? Results.NotFound() : Results.Ok(ToJobDto(job));
});

app.MapHub<ConnectorHub>("/hubs/connector");
app.Run();

static JobDto ToJobDto(JobRecord x) => new()
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
