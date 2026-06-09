using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using WizAccountant.Api;
using WizAccountant.Api.Act;
using WizAccountant.Api.Insight;
using WizAccountant.Api.Middleware;
using WizAccountant.Contracts;

var builder = WebApplication.CreateBuilder(args);

// G5: QuestPDF license — set to Professional in production via env var QUESTPDF_LICENSE=Professional
QuestPDF.Settings.License = (Environment.GetEnvironmentVariable("QUESTPDF_LICENSE") ?? "Community")
    .Equals("Professional", StringComparison.OrdinalIgnoreCase)
    ? QuestPDF.Infrastructure.LicenseType.Professional
    : QuestPDF.Infrastructure.LicenseType.Community;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache(opt => opt.SizeLimit = 1024);

// Auth — JWT Bearer (validates tokens issued by WizTokenService)
builder.Services.AddSingleton<WizTokenService>();
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "WizAccountant",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Issuer"] ?? "WizAccountant",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });
    builder.Services.AddAuthorization();
}

builder.Services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ReadOnlyChatService>();
builder.Services.AddScoped<InsightQueryLogService>();
builder.Services.AddScoped<InsightTriageService>();
builder.Services.AddScoped<InsightSqlQueryService>();
builder.Services.AddScoped<InsightSavedSqlQueryService>();
builder.Services.AddScoped<NotificationStubService>();
builder.Services.AddScoped<WizNotificationService>();
builder.Services.AddSingleton<SmtpEmailService>();  // MC2
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddSingleton<ActDraftService>();
builder.Services.AddScoped<SiteConfigService>();
builder.Services.AddScoped<FirmService>();
builder.Services.AddScoped<SiteMonitorService>();
builder.Services.AddScoped<MultiSiteQueryService>();
builder.Services.AddSingleton<LocalConnectorLauncher>();
// Phase 4 Block 4 — SSO + Billing + Compliance
builder.Services.Configure<OidcSettings>(builder.Configuration.GetSection("Oidc"));
builder.Services.AddHttpClient("OidcJwks");
builder.Services.AddHttpClient("ExpoPush");  // M4: Expo push notification client
builder.Services.AddSingleton<IOidcTokenValidator, OidcTokenValidator>();
builder.Services.AddScoped<OidcAuthService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<ComplianceService>();
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
    // Apply EF Core migrations — creates/upgrades schema safely in production
    await db.Database.MigrateAsync();
    foreach (var site in db.Sites.Where(s => s.IsOnline))
        site.IsOnline = false;
    await db.SaveChangesAsync();
    await DbSeed.EnsurePhase1SeedAsync(db);
    await DbSeed.MigratePasswordHashesAsync(db);   // hash any plain-text passwords from pre-upgrade DBs
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("MobileDev");
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RbacMiddleware>();   // RBAC v2 — role enforcement after JWT validation

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<SiteRateLimitMiddleware>();

app.MapGet("/", () => Results.Redirect("/insight/index.html"));
app.MapGet("/admin", () => Results.Redirect("/admin/index.html"));
app.MapGet("/insight", () => Results.Redirect("/insight/index.html"));
app.MapGet("/act", () => Results.Redirect("/act/index.html"));
app.MapGet("/audit", () => Results.Redirect("/audit/index.html"));  // B5-D audit UI

app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    service = "WizAccountant.Api",
    insightChatVersion = InsightChatInfo.Version
}));

// MC4 — Connector version manifest: connectors poll this to check for updates
app.MapGet("/api/connector/version", () =>
{
    var apiVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
    // Current minimum compatible connector version — bump when breaking API changes land
    const string minimumConnectorVersion = "1.0.0.0";
    const string latestConnectorVersion  = "1.0.0.0";
    const string downloadUrl = "https://github.com/your-org/wizaccountant/releases/latest/download/WizConnectorSetup.ps1";
    const string releaseNotes = "https://github.com/your-org/wizaccountant/releases/latest";
    return Results.Ok(new
    {
        latestConnectorVersion,
        minimumConnectorVersion,
        downloadUrl,
        releaseNotes,
        apiVersion,
        publishedAtUtc = DateTimeOffset.UtcNow
    });
});

app.MapPost("/api/pairing-codes", async (CreatePairingCodeRequest request, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.SiteName))
        return Results.BadRequest("tenantId and siteName are required.");

    var code = $"WZ{Random.Shared.Next(100000, 999999)}";
    var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(request.ExpiresInMinutes, 5, 10080)); // max 7 days
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

app.MapGet("/api/insight/export/{jobId:guid}", async (Guid jobId, string? format, AppDbContext db, CancellationToken ct) =>
{
    var job = await db.Jobs.FindAsync([jobId], ct);
    if (job is null) return Results.NotFound();

    return (format?.ToLowerInvariant() ?? "csv") switch
    {
        "excel" or "xlsx" => ExportService.ToExcel(job.ResultJson) is { } xlsx
            ? Results.File(xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"wiz-export-{jobId:N}.xlsx")
            : Results.BadRequest("Job result is not exportable."),

        "pdf" => ExportService.ToPdf(job.ResultJson) is { } pdf
            ? Results.File(pdf, "application/pdf", $"wiz-export-{jobId:N}.pdf")
            : Results.BadRequest("Job result is not exportable."),

        _ => ExportService.ToCsv(job.ResultJson) is { } csv
            ? Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"wiz-export-{jobId:N}.csv")
            : Results.BadRequest("Job result is not a list export.")
    };
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

// ── Firm management (Admin+) ─────────────────────────────────────────────────

app.MapGet("/api/firms", async (FirmService firms, CancellationToken ct) =>
    Results.Ok(await firms.ListAsync(ct)));

app.MapPost("/api/firms", async (CreateFirmRequest request, FirmService firms, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest("name is required.");
    var firm = await firms.CreateAsync(request, ct);
    return Results.Created($"/api/firms/{firm.FirmId}", firm);
});

app.MapGet("/api/firms/{firmId}", async (string firmId, FirmService firms, CancellationToken ct) =>
{
    var firm = await firms.GetAsync(firmId, ct);
    return firm is null ? Results.NotFound() : Results.Ok(firm);
});

app.MapPut("/api/firms/{firmId}", async (string firmId, UpdateFirmRequest request, FirmService firms, CancellationToken ct) =>
{
    var firm = await firms.UpdateAsync(firmId, request, ct);
    return firm is null ? Results.NotFound() : Results.Ok(firm);
});

app.MapGet("/api/firms/{firmId}/tenants", async (string firmId, FirmService firms, CancellationToken ct) =>
    Results.Ok(await firms.ListTenantsAsync(firmId, ct)));

app.MapPost("/api/firms/{firmId}/tenants/{tenantId}", async (string firmId, string tenantId, FirmService firms, CancellationToken ct) =>
{
    var ok = await firms.AssignTenantAsync(firmId, tenantId, ct);
    return ok ? Results.NoContent() : Results.NotFound($"Tenant {tenantId} not found.");
});

// ── Mobile Phase 4 — practice mode + layout preferences (Reader+) ────────────

app.MapGet("/api/mobile/app-config", async (string? tenantId, AppDbContext appDb, CancellationToken ct) =>
{
    // Returns mobile app configuration: practice mode, enabled features, layout hint
    var practiceMode = false;
    string? firmId = null;
    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        var tenant = await appDb.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        firmId = tenant?.FirmId;
        if (firmId is not null)
        {
            var firm = await appDb.Firms.AsNoTracking()
                .FirstOrDefaultAsync(f => f.FirmId == firmId, ct);
            practiceMode = firm?.IsPracticeMode == true;
        }
    }

    return Results.Ok(new
    {
        practiceMode,
        firmId,
        features = new
        {
            insightChat = true,
            inventoryRead = true,
            actProposals = !practiceMode,
            tabletLayout = true
        },
        inventoryOperations = new[]
        {
            "inventory.value.top", "inventory.below.reorder", "inventory.slow.moving.top",
            "inventory.negative.qty", "inventory.movement.top", "warehouse.value.summary"
        }
    });
});

// ── Multi-company cross-site query (Reader+) ─────────────────────────────────

app.MapPost("/api/insight/multi-site/query", async (
    MultiSiteQueryRequest request,
    MultiSiteQueryService multiSite,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Operation))
        return Results.BadRequest("operation is required.");
    if (string.IsNullOrWhiteSpace(request.FirmId) && string.IsNullOrWhiteSpace(request.TenantId))
        return Results.BadRequest("firmId or tenantId is required.");

    var timeout = Math.Clamp(request.TimeoutSeconds, 10, 120);
    MultiSiteQueryResult result;
    if (!string.IsNullOrWhiteSpace(request.FirmId))
        result = await multiSite.QueryFirmAsync(request.FirmId, request.Operation, request.Parameters, timeout, ct);
    else
        result = await multiSite.QueryTenantAsync(request.TenantId!, request.Operation, request.Parameters, timeout, ct);

    return Results.Ok(result);
});

// ── Monitoring (Approver+) ────────────────────────────────────────────────────

app.MapGet("/api/monitor/sites", async (SiteMonitorService monitor, CancellationToken ct) =>
    Results.Ok(await monitor.GetSiteSlaAsync(ct)));

app.MapGet("/api/monitor/sites/{siteId:guid}/alerts", async (Guid siteId, SiteMonitorService monitor, CancellationToken ct) =>
    Results.Ok(await monitor.GetSiteAlertsAsync(siteId, ct)));

// ── Phase 4 Block 4: SSO, Billing, Compliance ─────────────────────────────

// POST /api/auth/oidc/login — exchange external id_token for WizAccountant JWT
app.MapPost("/api/auth/oidc/login", async (OidcLoginRequest request, OidcAuthService oidc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.IdToken))
        return Results.BadRequest(new { error = "provider and idToken are required." });
    var response = await oidc.LoginAsync(request, ct);
    return response is null
        ? Results.Unauthorized()
        : Results.Ok(response);
});

// POST /api/push-tokens — M4: mobile app registers Expo push token
app.MapPost("/api/push-tokens", async (
    RegisterPushTokenRequest request,
    AppDbContext db,
    Microsoft.AspNetCore.Http.HttpContext ctx,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Platform))
        return Results.BadRequest("token and platform are required.");

    // Upsert — one record per (userId, token)
    var existing = await db.PushTokens.FirstOrDefaultAsync(
        p => p.UserId == request.UserId && p.Token == request.Token, ct);

    if (existing is not null)
    {
        existing.LastSeenAtUtc = DateTimeOffset.UtcNow;
    }
    else
    {
        db.PushTokens.Add(new PushTokenRecord
        {
            PushTokenId = Guid.NewGuid(),
            UserId = request.UserId,
            Token = request.Token.Trim(),
            Platform = request.Platform.Trim(),
            RegisteredAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        });
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { registered = true });
});

// POST /api/billing/webhook — receive Stripe/Paddle webhook events
app.MapPost("/api/billing/webhook", async (
    HttpRequest req,
    IConfiguration config,
    BillingService billing,
    CancellationToken ct) =>
{
    // Read raw body first (needed for signature verification)
    req.EnableBuffering();
    using var reader = new System.IO.StreamReader(req.Body, leaveOpen: true);
    var rawBody = await reader.ReadToEndAsync(ct);
    req.Body.Position = 0;

    // Stripe signature verification
    var stripeHeader = req.Headers["Stripe-Signature"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(stripeHeader))
    {
        var stripeSecret = config["Billing:StripeWebhookSecret"];
        if (!string.IsNullOrWhiteSpace(stripeSecret) &&
            !BillingWebhookVerifier.VerifyStripe(rawBody, stripeHeader, stripeSecret))
            return Results.Unauthorized();
    }

    // Paddle signature verification
    var paddleHeader = req.Headers["Paddle-Signature"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(paddleHeader))
    {
        var paddleSecret = config["Billing:PaddleWebhookSecret"];
        if (!string.IsNullOrWhiteSpace(paddleSecret) &&
            !BillingWebhookVerifier.VerifyPaddle(rawBody, paddleHeader, paddleSecret))
            return Results.Unauthorized();
    }

    string eventType;
    System.Text.Json.JsonDocument payload;
    try
    {
        payload = await System.Text.Json.JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
        eventType = payload.RootElement.TryGetProperty("type", out var t)
            ? t.GetString() ?? "unknown"
            : payload.RootElement.TryGetProperty("event_type", out var e2) ? e2.GetString() ?? "unknown" : "unknown";
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid JSON payload." });
    }
    await billing.HandleWebhookAsync(eventType, payload, ct);
    return Results.Ok(new { received = true });
});

// GET /api/billing/subscription/{tenantId} — Admin: view subscription state
app.MapGet("/api/billing/subscription/{tenantId}", async (string tenantId, BillingService billing, CancellationToken ct) =>
    Results.Ok(await billing.GetSubscriptionAsync(tenantId, ct)));

// GET /api/compliance/data-export?tenantId=xxx — Admin: POPIA/GDPR data export
app.MapGet("/api/compliance/data-export", async (string tenantId, ComplianceService compliance, CancellationToken ct) =>
{
    try
    {
        var export = await compliance.ExportTenantDataAsync(tenantId, ct);
        return Results.Ok(export);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

// DELETE /api/compliance/users/{userId} — FirmAdmin: right to erasure (redact PII)
app.MapDelete("/api/compliance/users/{userId:guid}", async (Guid userId, ComplianceService compliance, CancellationToken ct) =>
{
    try
    {
        await compliance.RedactUserAsync(userId, ct);
        return Results.Ok(new { redacted = true, userId });
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
});

app.MapHub<ConnectorHub>("/hubs/connector");
app.MapHub<UiNotificationHub>("/hubs/ui");  // B5-B: real-time push to browser clients
app.Run();

// Expose Program to integration tests via InternalsVisibleTo
public partial class Program { }
