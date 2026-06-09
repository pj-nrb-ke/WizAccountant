using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WizAccountant.Api;
using WizAccountant.Contracts;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Phase 4 Block 4 — SSO (Task #18) and Billing/Compliance (Task #19) tests.
/// Covers: OidcSettings, WizRoles paths (oidc/billing/compliance), OidcAuthService,
/// BillingService subscription logic, ComplianceService data export/redaction.
/// </summary>
public sealed class SsoAndBillingTests : IDisposable
{
    private readonly AppDbContext _db;

    public SsoAndBillingTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── OidcSettings ────────────────────────────────────────────────────────

    [Fact]
    public void OidcSettings_GetProvider_FindsByName_CaseInsensitive()
    {
        var settings = new OidcSettings
        {
            Providers =
            [
                new OidcProviderConfig
                {
                    Provider = "AzureAD",
                    Issuer = "https://login.microsoftonline.com/tenant-id/v2.0",
                    ClientId = "client-123",
                    JwksUri = "https://login.microsoftonline.com/tenant-id/discovery/v2.0/keys",
                },
                new OidcProviderConfig
                {
                    Provider = "Google",
                    Issuer = "https://accounts.google.com",
                    ClientId = "google-client.apps.googleusercontent.com",
                    JwksUri = "https://www.googleapis.com/oauth2/v3/certs",
                },
            ]
        };

        Assert.NotNull(settings.GetProvider("AzureAD"));
        Assert.NotNull(settings.GetProvider("azuread"));   // case-insensitive
        Assert.NotNull(settings.GetProvider("GOOGLE"));
        Assert.Null(settings.GetProvider("Okta"));
    }

    [Fact]
    public void OidcProviderConfig_IsValid_RequiresAllFields()
    {
        var valid = new OidcProviderConfig
        {
            Provider = "Google",
            Issuer = "https://accounts.google.com",
            ClientId = "client.apps.googleusercontent.com",
            JwksUri = "https://www.googleapis.com/oauth2/v3/certs",
        };
        Assert.True(valid.IsValid());

        Assert.False(new OidcProviderConfig { Issuer = "x", ClientId = "x", JwksUri = "x" }.IsValid()); // missing provider
        Assert.False(new OidcProviderConfig { Provider = "x", ClientId = "x", JwksUri = "x" }.IsValid()); // missing issuer
        Assert.False(new OidcProviderConfig { Provider = "x", Issuer = "x", JwksUri = "x" }.IsValid()); // missing clientId
        Assert.False(new OidcProviderConfig { Provider = "x", Issuer = "x", ClientId = "x" }.IsValid()); // missing jwksUri
    }

    [Fact]
    public void OidcProviderConfig_DefaultRole_IsReader()
    {
        var config = new OidcProviderConfig();
        Assert.Equal("Reader", config.DefaultRole);
    }

    // ── WizRoles — OIDC / billing / compliance paths ─────────────────────────

    [Fact]
    public void WizRoles_OidcLogin_IsPublic()
    {
        Assert.Null(WizRoles.MinimumRoleFor("POST", "/api/auth/oidc/login"));
    }

    [Fact]
    public void WizRoles_BillingWebhook_IsPublic()
    {
        Assert.Null(WizRoles.MinimumRoleFor("POST", "/api/billing/webhook"));
    }

    [Fact]
    public void WizRoles_BillingSubscription_RequiresAdmin()
    {
        Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("GET", "/api/billing/subscription/pilot-tenant"));
    }

    [Fact]
    public void WizRoles_ComplianceDataExport_RequiresAdmin()
    {
        Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("GET", "/api/compliance/data-export"));
    }

    [Fact]
    public void WizRoles_ComplianceUserRedact_RequiresFirmAdmin()
    {
        var id = Guid.NewGuid();
        Assert.Equal(WizRoles.FirmAdmin, WizRoles.MinimumRoleFor("DELETE", $"/api/compliance/users/{id}"));
    }

    // ── OidcAuthService — auto-provision + link ──────────────────────────────

    [Fact]
    public async Task OidcAuthService_Login_CreatesNewUser_WhenNoExistingUserOrIdentity()
    {
        _db.Tenants.Add(new TenantRecord { TenantId = "tenant-x", Name = "Test Org" });
        await _db.SaveChangesAsync();

        var tokens = new WizTokenService(BuildTestConfig());
        var validator = new AlwaysValidOidcValidator(
            new OidcClaims("Google", "google-sub-123", "newuser@test.org", "New User"));
        var service = new OidcAuthService(_db, validator, tokens, NullLogger<OidcAuthService>.Instance);

        var result = await service.LoginAsync(
            new OidcLoginRequest { Provider = "Google", IdToken = "fake-token" },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("New User", result!.DisplayName);
        Assert.Equal("Reader", result.Role);
        // User should now exist in DB
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "newuser@test.org");
        Assert.NotNull(user);
        // External identity link should be created
        var link = await _db.ExternalIdentities.FirstOrDefaultAsync(e => e.Subject == "google-sub-123");
        Assert.NotNull(link);
        Assert.Equal("Google", link!.Provider);
        Assert.Equal(user!.UserId, link.UserId);
    }

    [Fact]
    public async Task OidcAuthService_Login_ReusesExistingUserByEmail()
    {
        var existingUserId = Guid.NewGuid();
        _db.Tenants.Add(new TenantRecord { TenantId = "t1", Name = "Org" });
        _db.Users.Add(new UserRecord
        {
            UserId = existingUserId,
            TenantId = "t1",
            Email = "existing@org.com",
            DisplayName = "Existing User",
            Role = "Preparer",
            Password = string.Empty,
        });
        await _db.SaveChangesAsync();

        var tokens = new WizTokenService(BuildTestConfig());
        var validator = new AlwaysValidOidcValidator(
            new OidcClaims("AzureAD", "azure-sub-999", "existing@org.com", "Existing User"));
        var service = new OidcAuthService(_db, validator, tokens, NullLogger<OidcAuthService>.Instance);

        var result = await service.LoginAsync(
            new OidcLoginRequest { Provider = "AzureAD", IdToken = "fake-token" },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(existingUserId, result!.UserId);
        Assert.Equal("Preparer", result.Role);
        // Link should have been created for the existing user
        var link = await _db.ExternalIdentities.FirstOrDefaultAsync(e => e.Subject == "azure-sub-999");
        Assert.NotNull(link);
        Assert.Equal(existingUserId, link!.UserId);
    }

    [Fact]
    public async Task OidcAuthService_Login_ReturnsNull_WhenValidatorFails()
    {
        var tokens = new WizTokenService(BuildTestConfig());
        var validator = new AlwaysFailingOidcValidator();
        var service = new OidcAuthService(_db, validator, tokens, NullLogger<OidcAuthService>.Instance);

        var result = await service.LoginAsync(
            new OidcLoginRequest { Provider = "Google", IdToken = "bad-token" },
            CancellationToken.None);

        Assert.Null(result);
    }

    // ── BillingService ───────────────────────────────────────────────────────

    [Fact]
    public async Task BillingService_GetSubscription_ReturnsFree_WhenNoRecord()
    {
        var svc = new BillingService(_db, NullLogger<BillingService>.Instance);
        var sub = await svc.GetSubscriptionAsync("nonexistent-tenant", CancellationToken.None);
        Assert.Equal("free", sub.Plan);
        Assert.Equal("active", sub.Status);
    }

    [Fact]
    public async Task BillingService_GetSubscription_ReturnsRecord_WhenExists()
    {
        _db.Subscriptions.Add(new SubscriptionRecord
        {
            TenantId = "t-billing",
            Plan = "enterprise",
            Status = "active",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var svc = new BillingService(_db, NullLogger<BillingService>.Instance);
        var sub = await svc.GetSubscriptionAsync("t-billing", CancellationToken.None);
        Assert.Equal("enterprise", sub.Plan);
        Assert.Equal("active", sub.Status);
    }

    [Fact]
    public async Task BillingService_IsFeatureEnabled_ProIncludesInsightChat()
    {
        _db.Subscriptions.Add(new SubscriptionRecord
        {
            TenantId = "t-pro",
            Plan = "pro",
            Status = "active",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var svc = new BillingService(_db, NullLogger<BillingService>.Instance);
        Assert.True(await svc.IsFeatureEnabledAsync("t-pro", "insight.chat", CancellationToken.None));
        Assert.False(await svc.IsFeatureEnabledAsync("t-pro", "sso", CancellationToken.None));
    }

    [Fact]
    public async Task BillingService_IsFeatureEnabled_EnterpriseIncludesAll()
    {
        _db.Subscriptions.Add(new SubscriptionRecord
        {
            TenantId = "t-ent",
            Plan = "enterprise",
            Status = "active",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var svc = new BillingService(_db, NullLogger<BillingService>.Instance);
        Assert.True(await svc.IsFeatureEnabledAsync("t-ent", "sso", CancellationToken.None));
        Assert.True(await svc.IsFeatureEnabledAsync("t-ent", "multisite", CancellationToken.None));
        Assert.True(await svc.IsFeatureEnabledAsync("t-ent", "insight.chat", CancellationToken.None));
    }

    [Fact]
    public async Task BillingService_IsFeatureEnabled_PastDueBlocks()
    {
        _db.Subscriptions.Add(new SubscriptionRecord
        {
            TenantId = "t-pastdue",
            Plan = "pro",
            Status = "past_due",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var svc = new BillingService(_db, NullLogger<BillingService>.Instance);
        Assert.False(await svc.IsFeatureEnabledAsync("t-pastdue", "insight.chat", CancellationToken.None));
    }

    [Fact]
    public async Task BillingService_HandleWebhook_CancelledSetsStatusCancelled()
    {
        _db.Subscriptions.Add(new SubscriptionRecord
        {
            TenantId = "t-cancel",
            Plan = "pro",
            Status = "active",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var svc = new BillingService(_db, NullLogger<BillingService>.Instance);
        var payload = System.Text.Json.JsonDocument.Parse("""
            {
              "type": "customer.subscription.deleted",
              "id": "sub_123",
              "metadata": { "wizTenantId": "t-cancel" }
            }
            """);

        await svc.HandleWebhookAsync("customer.subscription.deleted", payload, CancellationToken.None);

        var sub = await _db.Subscriptions.FindAsync(["t-cancel"]);
        Assert.Equal("cancelled", sub!.Status);
    }

    // ── ComplianceService ────────────────────────────────────────────────────

    [Fact]
    public async Task ComplianceService_ExportTenantData_ReturnsCorrectCounts()
    {
        _db.Tenants.Add(new TenantRecord { TenantId = "comp-t", Name = "Compliance Co" });
        _db.Users.Add(new UserRecord
        {
            UserId = Guid.NewGuid(), TenantId = "comp-t",
            Email = "user@comp.co", DisplayName = "User", Role = "Reader", Password = "x"
        });
        await _db.SaveChangesAsync();

        var svc = new ComplianceService(_db, NullLogger<ComplianceService>.Instance);
        var export = await svc.ExportTenantDataAsync("comp-t", CancellationToken.None);

        Assert.Equal("comp-t", export.TenantId);
        Assert.Equal("Compliance Co", export.TenantName);
        Assert.Single(export.Users);
        Assert.Equal("user@comp.co", export.Users[0].Email);
    }

    [Fact]
    public async Task ComplianceService_ExportTenantData_ThrowsForUnknownTenant()
    {
        var svc = new ComplianceService(_db, NullLogger<ComplianceService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ExportTenantDataAsync("ghost-tenant", CancellationToken.None));
    }

    [Fact]
    public async Task ComplianceService_RedactUser_ReplacesEmailAndName()
    {
        var userId = Guid.NewGuid();
        _db.Tenants.Add(new TenantRecord { TenantId = "rt", Name = "Redact Org" });
        _db.Users.Add(new UserRecord
        {
            UserId = userId, TenantId = "rt",
            Email = "pii@personal.com", DisplayName = "Real Name",
            Role = "Reader", Password = "hashed-pw"
        });
        await _db.SaveChangesAsync();

        var svc = new ComplianceService(_db, NullLogger<ComplianceService>.Instance);
        await svc.RedactUserAsync(userId, CancellationToken.None);

        var user = await _db.Users.FindAsync([userId]);
        Assert.Contains("redacted", user!.Email);
        Assert.Equal("[Redacted]", user.DisplayName);
        Assert.Empty(user.Password);
    }

    private static IConfiguration BuildTestConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = new string('x', 32),
                ["Jwt:Issuer"] = "WizTest",
            })
            .Build();

    [Fact]
    public async Task ComplianceService_RedactUser_RemovesExternalIdentityLinks()
    {
        var userId = Guid.NewGuid();
        _db.Tenants.Add(new TenantRecord { TenantId = "rt2", Name = "Org" });
        _db.Users.Add(new UserRecord
        {
            UserId = userId, TenantId = "rt2",
            Email = "linked@sso.com", DisplayName = "SSO User",
            Role = "Reader", Password = string.Empty
        });
        _db.ExternalIdentities.Add(new ExternalIdentityRecord
        {
            ExternalIdentityId = Guid.NewGuid(),
            Provider = "Google", Subject = "sub-xyz",
            UserId = userId, ProviderEmail = "linked@sso.com",
            LinkedAtUtc = DateTimeOffset.UtcNow,
            LastLoginAtUtc = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var svc = new ComplianceService(_db, NullLogger<ComplianceService>.Instance);
        await svc.RedactUserAsync(userId, CancellationToken.None);

        var links = await _db.ExternalIdentities.Where(e => e.UserId == userId).ToListAsync();
        Assert.Empty(links);
    }
}

// ── Test doubles ─────────────────────────────────────────────────────────────

internal sealed class AlwaysValidOidcValidator(OidcClaims claims) : IOidcTokenValidator
{
    public Task<OidcClaims?> ValidateAsync(string provider, string idToken, CancellationToken ct = default) =>
        Task.FromResult<OidcClaims?>(claims);
}

internal sealed class AlwaysFailingOidcValidator : IOidcTokenValidator
{
    public Task<OidcClaims?> ValidateAsync(string provider, string idToken, CancellationToken ct = default) =>
        Task.FromResult<OidcClaims?>(null);
}
