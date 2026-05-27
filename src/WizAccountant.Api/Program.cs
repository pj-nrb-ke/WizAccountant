using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Api;
using WizAccountant.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<LocalConnectorLauncher>();
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS JobAuditRecords (
            AuditId TEXT NOT NULL PRIMARY KEY,
            JobId TEXT NOT NULL,
            SiteId TEXT NOT NULL,
            Operation TEXT NOT NULL,
            EventType TEXT NOT NULL,
            RequestedBy TEXT NULL,
            Success INTEGER NULL,
            Detail TEXT NULL,
            TimestampUtc TEXT NOT NULL
        );
        """);
    await DbSeed.EnsurePhase1SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/admin/index.html"));
app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "WizAccountant.Api" }));

app.MapPost("/api/pairing-codes", async (CreatePairingCodeRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.SiteName))
        return Results.BadRequest("tenantId and siteName are required.");

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

app.MapPost("/api/sites/pair", async (PairSiteRequest request, AppDbContext db) =>
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
    var sites = await db.Sites.OrderByDescending(s => s.LastSeenUtc ?? DateTimeOffset.MinValue)
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

app.MapPost("/api/jobs", async (CreateJobRequest request, JobService jobs, CancellationToken ct) =>
{
    try
    {
        var job = await jobs.CreateAndDispatchAsync(request, ct);
        return Results.Ok(JobService.ToJobDto(job));
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(ex.Message);
    }
});

/// <summary>P1-24: one call to submit and wait for job completion.</summary>
app.MapPost("/api/jobs/run-wait", async (RunJobWaitRequest request, JobService jobs, CancellationToken ct) =>
{
    try
    {
        var result = await jobs.RunAndWaitAsync(new CreateJobRequest
        {
            SiteId = request.SiteId,
            Operation = request.Operation,
            Parameters = request.Parameters,
            RequestedBy = request.RequestedBy ?? "api"
        }, request.TimeoutSeconds, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (TimeoutException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status504GatewayTimeout);
    }
});

app.MapPost("/api/sites/{siteId:guid}/test-connection", async (Guid siteId, JobService jobs, CancellationToken ct) =>
{
    try
    {
        var result = await jobs.RunAndWaitAsync(new CreateJobRequest
        {
            SiteId = siteId,
            Operation = "site.health",
            Parameters = new Dictionary<string, string>(),
            RequestedBy = "admin-ui"
        }, 60, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(ex.Message);
    }
    catch (TimeoutException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status504GatewayTimeout);
    }
});

app.MapPost("/api/jobs/{jobId:guid}/result", async (Guid jobId, SubmitJobResultRequest result, JobService jobs, AppDbContext db, CancellationToken ct) =>
{
    await jobs.RecordResultAsync(jobId, result, ct);
    var job = await db.Jobs.FindAsync([jobId], ct);
    return job is null ? Results.NotFound() : Results.Ok(JobService.ToJobDto(job));
});

app.MapGet("/api/jobs/{jobId:guid}", async (Guid jobId, AppDbContext db, CancellationToken ct) =>
{
    var job = await db.Jobs.FindAsync([jobId], ct);
    return job is null ? Results.NotFound() : Results.Ok(JobService.ToJobDto(job));
});

/// <summary>P1-25: recent job activity for admin review.</summary>
app.MapGet("/api/audit/jobs", async (int? take, JobService jobs, CancellationToken ct) =>
    Results.Ok(await jobs.ListAuditAsync(take ?? 50, ct)));

/// <summary>P1-22: minimal auth stub for Phase 1.</summary>
app.MapPost("/api/auth/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
{
    var result = await auth.LoginAsync(request, ct);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

app.MapGet("/api/auth/tenants", async (AuthService auth, CancellationToken ct) =>
    Results.Ok(await auth.ListTenantsAsync(ct)));

app.MapGet("/api/auth/users", async (string? tenantId, AuthService auth, CancellationToken ct) =>
    Results.Ok(await auth.ListUsersAsync(tenantId, ct)));

/// <summary>P1-26: connector REST long-poll when SignalR is down.</summary>
app.MapGet("/api/connector/jobs/next", async (
    Guid siteId,
    string deviceId,
    int? waitSeconds,
    JobService jobs,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(deviceId))
        return Results.BadRequest("deviceId is required.");

    var result = await jobs.PollNextJobAsync(siteId, deviceId, waitSeconds ?? 30, ct);
    return result is null ? Results.NotFound("Site not found or device mismatch.") : Results.Ok(result);
});

/// <summary>Manager action: start connector service + tray on the same PC as this API (dev/pilot).</summary>
app.MapPost("/api/admin/start-local-programs", (LocalConnectorLauncher launcher, IHostEnvironment env, IConfiguration config) =>
{
    if (!env.IsDevelopment() && !config.GetValue("Admin:AllowLocalStart", false))
        return Results.Json(new { error = "Starting programs from the browser is only enabled on the pilot API." }, statusCode: 403);

    return Results.Ok(launcher.Start());
});

app.MapPost("/api/admin/open-sage-setup", (LocalConnectorLauncher launcher, IHostEnvironment env, IConfiguration config) =>
{
    if (!env.IsDevelopment() && !config.GetValue("Admin:AllowLocalStart", false))
        return Results.Json(new { error = "Opening Sage setup from the browser is only enabled on the pilot API." }, statusCode: 403);

    return Results.Ok(launcher.OpenSageSetup());
});

app.MapHub<ConnectorHub>("/hubs/connector");
app.Run();
