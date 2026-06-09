using WizAccountant.Api;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>
/// Tests for RBAC v2 — WizRoles hierarchy, HasAtLeast, and path-based minimum role rules.
/// </summary>
public sealed class RbacTests
{
    // ── Role level ordering ───────────────────────────────────────────────────

    [Fact]
    public void Levels_ReaderLowest()
        => Assert.True(WizRoles.Level(WizRoles.Reader) < WizRoles.Level(WizRoles.Preparer));

    [Fact]
    public void Levels_PreparerLessThanApprover()
        => Assert.True(WizRoles.Level(WizRoles.Preparer) < WizRoles.Level(WizRoles.Approver));

    [Fact]
    public void Levels_ApproverLessThanAdmin()
        => Assert.True(WizRoles.Level(WizRoles.Approver) < WizRoles.Level(WizRoles.Admin));

    [Fact]
    public void Levels_AdminLessThanFirmAdmin()
        => Assert.True(WizRoles.Level(WizRoles.Admin) < WizRoles.Level(WizRoles.FirmAdmin));

    [Fact]
    public void Levels_UnknownRole_Zero()
        => Assert.Equal(0, WizRoles.Level("NotARealRole"));

    [Fact]
    public void Levels_NullRole_Zero()
        => Assert.Equal(0, WizRoles.Level(null));

    // ── HasAtLeast ────────────────────────────────────────────────────────────

    [Fact]
    public void HasAtLeast_SameRole_True()
        => Assert.True(WizRoles.HasAtLeast(WizRoles.Approver, WizRoles.Approver));

    [Fact]
    public void HasAtLeast_HigherRole_True()
        => Assert.True(WizRoles.HasAtLeast(WizRoles.Admin, WizRoles.Preparer));

    [Fact]
    public void HasAtLeast_FirmAdminMeetsAdmin_True()
        => Assert.True(WizRoles.HasAtLeast(WizRoles.FirmAdmin, WizRoles.Admin));

    [Fact]
    public void HasAtLeast_AdminMeetsReader_True()
        => Assert.True(WizRoles.HasAtLeast(WizRoles.Admin, WizRoles.Reader));

    [Fact]
    public void HasAtLeast_LowerRole_False()
        => Assert.False(WizRoles.HasAtLeast(WizRoles.Reader, WizRoles.Approver));

    [Fact]
    public void HasAtLeast_PreparerNotEnoughForApprover_False()
        => Assert.False(WizRoles.HasAtLeast(WizRoles.Preparer, WizRoles.Approver));

    [Fact]
    public void HasAtLeast_ReaderNotEnoughForAdmin_False()
        => Assert.False(WizRoles.HasAtLeast(WizRoles.Reader, WizRoles.Admin));

    [Fact]
    public void HasAtLeast_UnknownRole_False()
        => Assert.False(WizRoles.HasAtLeast("GhostRole", WizRoles.Reader));

    [Fact]
    public void HasAtLeast_NullRole_False()
        => Assert.False(WizRoles.HasAtLeast(null, WizRoles.Reader));

    // ── IsValid ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsValid_KnownRole_True()
        => Assert.True(WizRoles.IsValid(WizRoles.Reader));

    [Fact]
    public void IsValid_Unknown_False()
        => Assert.False(WizRoles.IsValid("SuperAdmin"));

    [Fact]
    public void IsValid_Null_False()
        => Assert.False(WizRoles.IsValid(null));

    [Fact]
    public void All_ContainsFiveRoles()
        => Assert.Equal(5, WizRoles.All.Count);

    // ── MinimumRoleFor — public paths (null = no enforcement) ─────────────────

    [Fact]
    public void MinRole_Login_Public()
        => Assert.Null(WizRoles.MinimumRoleFor("POST", "/api/auth/login"));

    [Fact]
    public void MinRole_Health_Public()
        => Assert.Null(WizRoles.MinimumRoleFor("GET", "/health"));

    [Fact]
    public void MinRole_ConnectorJobs_Public()
        => Assert.Null(WizRoles.MinimumRoleFor("GET", "/api/connector/jobs/next"));

    [Fact]
    public void MinRole_JobsResult_Public()
        => Assert.Null(WizRoles.MinimumRoleFor("POST", "/api/jobs/some-id/result"));

    [Fact]
    public void MinRole_PairingCode_Public()
        => Assert.Null(WizRoles.MinimumRoleFor("POST", "/api/pairing-codes"));

    [Fact]
    public void MinRole_SitesPair_Public()
        => Assert.Null(WizRoles.MinimumRoleFor("POST", "/api/sites/pair"));

    [Fact]
    public void MinRole_InsightTools_Public()
        => Assert.Null(WizRoles.MinimumRoleFor("GET", "/api/v1/insight/tools"));

    // ── MinimumRoleFor — Reader paths ─────────────────────────────────────────

    [Fact]
    public void MinRole_InsightChat_Reader()
        => Assert.Equal(WizRoles.Reader, WizRoles.MinimumRoleFor("POST", "/api/insight/chat"));

    [Fact]
    public void MinRole_InsightDashboard_Reader()
        => Assert.Equal(WizRoles.Reader, WizRoles.MinimumRoleFor("GET", "/api/insight/dashboard/some-site-id"));

    [Fact]
    public void MinRole_InsightConversations_Reader()
        => Assert.Equal(WizRoles.Reader, WizRoles.MinimumRoleFor("GET", "/api/insight/conversations"));

    [Fact]
    public void MinRole_SitesList_Reader()
        => Assert.Equal(WizRoles.Reader, WizRoles.MinimumRoleFor("GET", "/api/sites"));

    // ── MinimumRoleFor — Preparer paths ──────────────────────────────────────

    [Fact]
    public void MinRole_ActProposals_Preparer()
        => Assert.Equal(WizRoles.Preparer, WizRoles.MinimumRoleFor("POST", "/api/act/proposals"));

    [Fact]
    public void MinRole_ActAiDraft_Preparer()
        => Assert.Equal(WizRoles.Preparer, WizRoles.MinimumRoleFor("POST", "/api/act/ai-draft"));

    [Fact]
    public void MinRole_ActSyncConfig_Preparer()
        => Assert.Equal(WizRoles.Preparer, WizRoles.MinimumRoleFor("POST", "/api/act/sites/abc/sync-config"));

    // ── MinimumRoleFor — Approver paths ──────────────────────────────────────

    [Fact]
    public void MinRole_ApproveProposal_Approver()
        => Assert.Equal(WizRoles.Approver, WizRoles.MinimumRoleFor("POST", "/api/act/proposals/some-id/approve"));

    [Fact]
    public void MinRole_RejectProposal_Approver()
        => Assert.Equal(WizRoles.Approver, WizRoles.MinimumRoleFor("POST", "/api/act/proposals/some-id/reject"));

    [Fact]
    public void MinRole_WriteAudit_Approver()
        => Assert.Equal(WizRoles.Approver, WizRoles.MinimumRoleFor("GET", "/api/act/write-audit"));

    // ── MinimumRoleFor — Admin paths ──────────────────────────────────────────

    [Fact]
    public void MinRole_AdminStartPrograms_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("POST", "/api/admin/start-local-programs"));

    [Fact]
    public void MinRole_AuthUsers_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("GET", "/api/auth/users"));

    [Fact]
    public void MinRole_AuthTenants_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("GET", "/api/auth/tenants"));

    // ── Case-insensitive path matching ────────────────────────────────────────

    [Fact]
    public void MinRole_PathCaseInsensitive_InsightUppercase_Reader()
        => Assert.Equal(WizRoles.Reader, WizRoles.MinimumRoleFor("GET", "/API/INSIGHT/chat"));

    [Fact]
    public void MinRole_PathCaseInsensitive_AdminUppercase_Admin()
        => Assert.Equal(WizRoles.Admin, WizRoles.MinimumRoleFor("POST", "/API/ADMIN/start-local-programs"));

    // ── Role hierarchy gate integration ──────────────────────────────────────

    [Fact]
    public void Gate_ReaderCanAccessInsight()
        => Assert.True(WizRoles.HasAtLeast(WizRoles.Reader, WizRoles.MinimumRoleFor("POST", "/api/insight/chat")!));

    [Fact]
    public void Gate_PreparerCannotApprove()
        => Assert.False(WizRoles.HasAtLeast(
            WizRoles.Preparer,
            WizRoles.MinimumRoleFor("POST", "/api/act/proposals/id/approve")!));

    [Fact]
    public void Gate_ApproverCanApprove()
        => Assert.True(WizRoles.HasAtLeast(
            WizRoles.Approver,
            WizRoles.MinimumRoleFor("POST", "/api/act/proposals/id/approve")!));

    [Fact]
    public void Gate_AdminCanApprove()
        => Assert.True(WizRoles.HasAtLeast(
            WizRoles.Admin,
            WizRoles.MinimumRoleFor("POST", "/api/act/proposals/id/approve")!));

    [Fact]
    public void Gate_ReaderCannotCallAdmin()
        => Assert.False(WizRoles.HasAtLeast(
            WizRoles.Reader,
            WizRoles.MinimumRoleFor("POST", "/api/admin/start-local-programs")!));

    [Fact]
    public void Gate_FirmAdminCanDoEverything()
    {
        var paths = new[]
        {
            "/api/insight/chat",
            "/api/act/proposals",
            "/api/act/proposals/id/approve",
            "/api/act/write-audit",
            "/api/admin/start-local-programs",
            "/api/auth/users"
        };
        foreach (var path in paths)
        {
            var minRole = WizRoles.MinimumRoleFor("GET", path);
            if (minRole is not null)
                Assert.True(WizRoles.HasAtLeast(WizRoles.FirmAdmin, minRole), $"FirmAdmin should access {path}");
        }
    }
}
