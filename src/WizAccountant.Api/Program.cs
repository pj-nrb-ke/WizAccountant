using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Api;
using WizAccountant.Api.Act;
using WizAccountant.Api.Insight;
using WizAccountant.Api.Middleware;
using WizAccountant.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ReadOnlyChatService>();
builder.Services.AddScoped<InsightQueryLogService>();
builder.Services.AddScoped<InsightTriageService>();
builder.Services.AddScoped<InsightSqlQueryService>();
builder.Services.AddScoped<InsightSavedSqlQueryService>();
builder.Services.AddScoped<NotificationStubService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddSingleton<ActDraftService>();
builder.Services.AddScoped<SiteConfigService>();
builder.Services.AddSingleton<LocalConnectorLauncher>();
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("MobileDev", policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    foreach (var site in db.Sites.Where(s => s.IsOnline))
        site.IsOnline = false;
    await db.SaveChangesAsync();
    await DbSeed.EnsurePhase1SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("MobileDev");
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<SiteRateLimitMiddleware>();

app.MapGet("/", () => Results.Redirect("/insight/index.html"));
app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));
app.MapGet("/insight", () => Results.Redirect("/insight/index.html"));
app.MapGet("/act", () => Results.Redirect("/act/index.html"));

app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    service = "WizAccountant.Api",
    insightChatVersion = InsightChatInfo.Version
}));

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
    var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-90);
    // SQLite cannot ORDER BY DateTimeOffset — sort in memory after fetch.
    var rows = await db.Sites.AsNoTracking().ToListAsync();
    var sites = rows
        .OrderByDescending(s => s.LastSeenUtc ?? DateTimeOffset.MinValue)
        .Select(s => new SiteDto
        {
            SiteId = s.SiteId,
            TenantId = s.TenantId,
            SiteName = s.SiteName,
            DeviceId = s.DeviceId,
            IsOnline = s.IsOnline && s.LastSeenUtc.HasValue && s.LastSeenUtc.Value >= staleBefore,
            LastSeenUtc = s.LastSeenUtc
        })
        .ToList();
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

// --- Phase 2 Insight API (v1) ---
app.MapGet("/api/v1/insight/tools", () => Results.Ok(InsightReadOnlyTools.Allowed.OrderBy(x => x)));

app.MapGet("/api/insight/dashboard/{siteId:guid}", async (Guid siteId, JobService jobs, CancellationToken ct) =>
{
    try
    {
        var job = await jobs.RunAndWaitAsync(new CreateJobRequest
        {
            SiteId = siteId,
            Operation = "dashboard.summary",
            Parameters = new Dictionary<string, string>(),
            RequestedBy = "insight-ui"
        }, 90, ct);
        return Results.Ok(job);
    }
    catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
    catch (TimeoutException ex) { return Results.Json(new { error = ex.Message }, statusCode: 504); }
});

app.MapPost("/api/insight/search", async (InsightSearchRequest request, JobService jobs, CancellationToken ct) =>
{
    try
    {
        var job = await jobs.RunAndWaitAsync(new CreateJobRequest
        {
            SiteId = request.SiteId,
            Operation = "search.global",
            Parameters = new Dictionary<string, string> { ["query"] = request.Query },
            RequestedBy = "insight-ui"
        }, 60, ct);
        return Results.Ok(job);
    }
    catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
    catch (TimeoutException ex) { return Results.Json(new { error = ex.Message }, statusCode: 504); }
});

app.MapPost("/api/insight/sql", async (InsightSqlQueryRequest request, InsightSqlQueryService sqlQuery, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await sqlQuery.RunAsync(request, ct));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (TimeoutException ex) { return Results.Json(new { error = ex.Message }, statusCode: 504); }
});

app.MapGet("/api/insight/sql/invoice-lines-hint", async (Guid siteId, InsightSqlQueryService sqlQuery, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await sqlQuery.GetInvoiceLineHintAsync(siteId, ct));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (TimeoutException ex) { return Results.Json(new { error = ex.Message }, statusCode: 504); }
});

app.MapGet("/api/insight/sql/saved", async (
    Guid siteId,
    HttpContext http,
    InsightSavedSqlQueryService savedQueries,
    CancellationToken ct) =>
{
    var tenantId = http.Request.Headers.TryGetValue("X-Tenant-Id", out var t) ? t.ToString() : "pilot-tenant";
    try
    {
        return Results.Ok(await savedQueries.ListAsync(tenantId, siteId, ct));
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/insight/sql/saved", async (
    UpsertInsightSavedSqlQueryRequest request,
    HttpContext http,
    InsightSavedSqlQueryService savedQueries,
    CancellationToken ct) =>
{
    var tenantId = http.Request.Headers.TryGetValue("X-Tenant-Id", out var t) ? t.ToString() : "pilot-tenant";
    try
    {
        return Results.Ok(await savedQueries.UpsertAsync(tenantId, request, ct));
    }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapDelete("/api/insight/sql/saved/{queryId:guid}", async (
    Guid queryId,
    Guid siteId,
    HttpContext http,
    InsightSavedSqlQueryService savedQueries,
    CancellationToken ct) =>
{
    var tenantId = http.Request.Headers.TryGetValue("X-Tenant-Id", out var t) ? t.ToString() : "pilot-tenant";
    try
    {
        var deleted = await savedQueries.DeleteAsync(tenantId, siteId, queryId, ct);
        return deleted ? Results.NoContent() : Results.NotFound(new { error = "Saved query not found." });
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/insight/chat", async (
    ChatMessageRequest request,
    ReadOnlyChatService chat,
    HttpContext http,
    ILogger<ReadOnlyChatService> logger,
    CancellationToken ct) =>
{
    var tenantId = http.Request.Headers.TryGetValue("X-Tenant-Id", out var t) ? t.ToString() : "pilot-tenant";
    try
    {
        return Results.Ok(await chat.AskAsync(request, tenantId, ct));
    }
    catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
    catch (Exception ex)
    {
        logger.LogError(ex, "Insight chat failed");
        return Results.Ok(ReadOnlyChatService.BuildSafeErrorResponse(request.Message, ex));
    }
});

app.MapGet("/api/insight/conversations", async (Guid siteId, string? tenantId, ReadOnlyChatService chat, CancellationToken ct) =>
    Results.Ok(await chat.ListConversationsAsync(tenantId ?? "pilot-tenant", siteId, ct)));

app.MapPost("/api/insight/feedback", async (InsightFeedbackRequest request, InsightQueryLogService logs, CancellationToken ct) =>
{
    if (request.QueryLogId == Guid.Empty)
        return Results.BadRequest("QueryLogId required.");
    var (found, duplicate) = await logs.SetFeedbackAsync(
        request.QueryLogId, request.Rating, request.Reason, request.Note, ct);
    if (!found) return Results.NotFound();
    return Results.Ok(new { saved = !duplicate, duplicate });
});

app.MapGet("/api/insight/triage", async (string? tenantId, int? days, InsightTriageService triage, CancellationToken ct) =>
{
    var report = await triage.BuildReportAsync(tenantId ?? "pilot-tenant", days ?? 7, ct);
    return Results.Ok(new
    {
        report,
        markdown = triage.FormatMarkdown(report),
        candidateTestsJson = triage.ExportCandidateTestsJson(report)
    });
});

app.MapGet("/api/insight/export/{jobId:guid}", async (Guid jobId, AppDbContext db, CancellationToken ct) =>
{
    var job = await db.Jobs.FindAsync([jobId], ct);
    if (job is null) return Results.NotFound();
    var csv = ExportService.ToCsv(job.ResultJson);
    if (csv is null) return Results.BadRequest("Job result is not a list export.");
    return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"wiz-export-{jobId:N}.csv");
});

app.MapPost("/api/insight/notifications/stub", async (NotificationStubRequest request, NotificationStubService notifications, CancellationToken ct) =>
{
    try
    {
        await notifications.SendSiteEventAsync(request, ct);
        return Results.Ok(new { ok = true, message = "Notification logged (email stub — configure Brevo in production)." });
    }
    catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
});

// --- Phase 3 Act API ---
app.MapGet("/api/act/workflows", () => Results.Ok(WorkflowTemplates.All));

app.MapGet("/api/act/proposals", async (Guid? siteId, ApprovalStatus? status, ApprovalService approvals, CancellationToken ct) =>
    Results.Ok(await approvals.ListAsync(siteId, status, ct)));

app.MapPost("/api/act/proposals", async (ProposeApprovalRequest request, ApprovalService approvals, CancellationToken ct) =>
{
    try { return Results.Ok(await approvals.ProposeAsync(request, ct)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/act/proposals/{proposalId:guid}/approve", async (Guid proposalId, ApproveProposalRequest request, ApprovalService approvals, CancellationToken ct) =>
{
    try { return Results.Ok(await approvals.ApproveAsync(proposalId, request, ct)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
    catch (TimeoutException ex) { return Results.Json(new { error = ex.Message }, statusCode: 504); }
});

app.MapPost("/api/act/proposals/{proposalId:guid}/reject", async (Guid proposalId, RejectProposalRequest request, ApprovalService approvals, CancellationToken ct) =>
{
    try { return Results.Ok(await approvals.RejectAsync(proposalId, request, ct)); }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
});

app.MapPost("/api/act/ai-draft", (AiDraftRequest request, ActDraftService drafts) =>
    Results.Ok(drafts.CreateDraft(request)));

app.MapGet("/api/act/write-audit", async (Guid? siteId, ApprovalService approvals, CancellationToken ct) =>
    Results.Ok(await approvals.ListWriteAuditAsync(siteId, ct)));

app.MapPost("/api/act/sites/{siteId:guid}/sync-config", async (Guid siteId, SiteConfigService config, CancellationToken ct) =>
{
    try { return Results.Ok(await config.SyncAsync(siteId, ct)); }
    catch (InvalidOperationException ex) { return Results.NotFound(ex.Message); }
    catch (TimeoutException ex) { return Results.Json(new { error = ex.Message }, statusCode: 504); }
});

app.MapGet("/api/act/sites/{siteId:guid}/config", async (Guid siteId, SiteConfigService config, CancellationToken ct) =>
{
    var row = await config.GetAsync(siteId, ct);
    return row is null ? Results.NotFound() : Results.Ok(row);
});

app.MapHub<ConnectorHub>("/hubs/connector");
app.Run();
