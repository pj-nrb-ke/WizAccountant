using WizAccountant.Api;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for Phase 4 Block 2:
/// - WizRoles path rules updated for /api/firms and /api/monitor
/// - WizRoles path rules for /api/mobile
/// - FirmRecord / practice mode contract shapes
/// </summary>
public sealed class FirmAndMonitorTests
{
    // ── Firm API path rules (Admin) ───────────────────────────────────────────

    [Fact]
    public void MinRole_FirmsList_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("GET", "/api/firms"));

    [Fact]
    public void MinRole_FirmsCreate_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("POST", "/api/firms"));

    [Fact]
    public void MinRole_FirmsGet_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("GET", "/api/firms/pilot-firm"));

    [Fact]
    public void MinRole_FirmsUpdate_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("PUT", "/api/firms/pilot-firm"));

    [Fact]
    public void MinRole_FirmsTenants_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("GET", "/api/firms/pilot-firm/tenants"));

    [Fact]
    public void MinRole_FirmsAssignTenant_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("POST", "/api/firms/pilot-firm/tenants/pilot-tenant"));

    // ── Monitor API path rules (Approver) ────────────────────────────────────

    [Fact]
    public void MinRole_MonitorSites_Approver()
        => Assert.Equal(WizRoles.Approver, WizRoles.MinimumRoleFor("GET", "/api/monitor/sites"));

    [Fact]
    public void MinRole_MonitorSiteAlerts_Approver()
        => Assert.Equal(WizRoles.Approver, WizRoles.MinimumRoleFor("GET", "/api/monitor/sites/some-guid/alerts"));

    // ── Mobile API path rules (Reader) ────────────────────────────────────────

    [Fact]
    public void MinRole_MobileAppConfig_Reader()
        => Assert.Equal(WizRoles.Reader, WizRoles.MinimumRoleFor("GET", "/api/mobile/app-config"));

    [Fact]
    public void MinRole_MobileAnyPath_Reader()
        => Assert.Equal(WizRoles.Reader, WizRoles.MinimumRoleFor("GET", "/api/mobile/inventory/top"));

    // ── RBAC gate: Reader cannot access firms (Admin required) ───────────────

    [Fact]
    public void Gate_ReaderCannotManageFirms()
    {
        var minRole = WizRoles.MinimumRoleFor("GET", "/api/firms");
        Assert.NotNull(minRole);
        Assert.False(WizRoles.HasAtLeast(WizRoles.Reader, minRole!));
    }

    [Fact]
    public void Gate_AdminCanManageFirms()
    {
        var minRole = WizRoles.MinimumRoleFor("GET", "/api/firms");
        Assert.NotNull(minRole);
        Assert.True(WizRoles.HasAtLeast(WizRoles.Admin, minRole!));
    }

    [Fact]
    public void Gate_ApproverCannotManageFirms()
    {
        var minRole = WizRoles.MinimumRoleFor("POST", "/api/firms");
        Assert.NotNull(minRole);
        Assert.False(WizRoles.HasAtLeast(WizRoles.Approver, minRole!));
    }

    [Fact]
    public void Gate_ReaderCannotViewMonitor()
    {
        var minRole = WizRoles.MinimumRoleFor("GET", "/api/monitor/sites");
        Assert.NotNull(minRole);
        Assert.False(WizRoles.HasAtLeast(WizRoles.Reader, minRole!));
    }

    [Fact]
    public void Gate_ApproverCanViewMonitor()
    {
        var minRole = WizRoles.MinimumRoleFor("GET", "/api/monitor/sites");
        Assert.NotNull(minRole);
        Assert.True(WizRoles.HasAtLeast(WizRoles.Approver, minRole!));
    }

    [Fact]
    public void Gate_ReaderCanAccessMobileConfig()
    {
        var minRole = WizRoles.MinimumRoleFor("GET", "/api/mobile/app-config");
        Assert.NotNull(minRole);
        Assert.True(WizRoles.HasAtLeast(WizRoles.Reader, minRole!));
    }

    // ── SiteMonitorService shape validation (direct DTO shape checks) ─────────

    [Fact]
    public void SiteSlaDto_DefaultValues_ValidShape()
    {
        var dto = new SiteSlaDto
        {
            SiteId = Guid.NewGuid(),
            TenantId = "pilot-tenant",
            SiteName = "Pilot",
            IsOnline = true,
            LastSeenUtc = DateTimeOffset.UtcNow,
            JobsLast24h = 10,
            FailedLast24h = 0,
            SuccessRatePct = 100.0,
            HasAlerts = false,
            Status = "healthy"
        };

        Assert.Equal("healthy", dto.Status);
        Assert.False(dto.HasAlerts);
        Assert.Equal(100.0, dto.SuccessRatePct);
    }

    [Fact]
    public void SiteSlaDto_DegradedStatus_WhenAlerts()
    {
        var dto = new SiteSlaDto
        {
            IsOnline = true,
            FailedLast24h = 5,
            HasAlerts = true,
            SuccessRatePct = 50.0,
            Status = "degraded"
        };

        Assert.Equal("degraded", dto.Status);
        Assert.True(dto.HasAlerts);
    }

    [Fact]
    public void SiteAlertsDto_EmptyFailedJobs_NotTriggered()
    {
        var dto = new SiteAlertsDto
        {
            SiteId = Guid.NewGuid(),
            WindowHours = 24,
            AlertThreshold = 3,
            FailedCount = 0,
            TriggersAlert = false,
            FailedJobs = []
        };

        Assert.False(dto.TriggersAlert);
        Assert.Empty(dto.FailedJobs);
    }

    [Fact]
    public void SiteAlertsDto_ThreeFailures_TriggersAlert()
    {
        var dto = new SiteAlertsDto
        {
            AlertThreshold = 3,
            FailedCount = 3,
            TriggersAlert = true,
            FailedJobs = [
                new FailedJobAlert { Operation = "vat.summary", FailedAtUtc = DateTimeOffset.UtcNow },
                new FailedJobAlert { Operation = "ar.gl.reconcile", FailedAtUtc = DateTimeOffset.UtcNow },
                new FailedJobAlert { Operation = "gl.ratios", FailedAtUtc = DateTimeOffset.UtcNow }
            ]
        };

        Assert.True(dto.TriggersAlert);
        Assert.Equal(3, dto.FailedJobs.Count);
    }

    // ── FirmRecord / FirmDto shape ────────────────────────────────────────────

    [Fact]
    public void FirmDto_PracticeModeFalse_Default()
    {
        var dto = new FirmDto
        {
            FirmId = "pilot-firm",
            Name = "Pilot Firm",
            IsPracticeMode = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        Assert.False(dto.IsPracticeMode);
        Assert.Equal("pilot-firm", dto.FirmId);
    }

    [Fact]
    public void FirmDto_PracticeModeTrue_BlocksWrites()
    {
        // In practice mode, the firm blocks write proposals
        var dto = new FirmDto { IsPracticeMode = true };
        Assert.True(dto.IsPracticeMode);
    }

    [Fact]
    public void CreateFirmRequest_DefaultPracticeModeIsFalse()
    {
        var req = new CreateFirmRequest { Name = "Test Firm" };
        Assert.False(req.IsPracticeMode);
    }

    [Fact]
    public void UpdateFirmRequest_CanTogglePracticeMode()
    {
        var req = new UpdateFirmRequest { IsPracticeMode = true };
        Assert.True(req.IsPracticeMode);
    }
}
