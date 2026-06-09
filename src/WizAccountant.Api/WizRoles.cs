namespace WizAccountant.Api;

/// <summary>
/// Fine-grained role model for RBAC v2.
/// Hierarchy: Reader &lt; Preparer &lt; Approver &lt; Admin &lt; FirmAdmin.
/// </summary>
public static class WizRoles
{
    // Role name constants — stored in UserRecord.Role and JWT "role" claim.
    public const string Reader = "Reader";
    public const string Preparer = "Preparer";
    public const string Approver = "Approver";
    public const string Admin = "Admin";
    public const string FirmAdmin = "FirmAdmin";

    public static readonly IReadOnlyList<string> All = [Reader, Preparer, Approver, Admin, FirmAdmin];

    private static readonly Dictionary<string, int> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        [Reader]    = 1,
        [Preparer]  = 2,
        [Approver]  = 3,
        [Admin]     = 4,
        [FirmAdmin] = 5,
    };

    /// <summary>Returns the numeric level for a role name. Unknown roles return 0.</summary>
    public static int Level(string? role) =>
        role is not null && Levels.TryGetValue(role, out var l) ? l : 0;

    /// <summary>Returns true if userRole meets or exceeds requiredRole in the hierarchy.</summary>
    public static bool HasAtLeast(string? userRole, string requiredRole) =>
        Level(userRole) >= Level(requiredRole);

    /// <summary>Returns true if the role string is a known WizRoles value.</summary>
    public static bool IsValid(string? role) =>
        role is not null && Levels.ContainsKey(role);

    /// <summary>
    /// Returns the minimum role required for the given HTTP method + path,
    /// or null if the endpoint is public (no role enforcement).
    /// Called by RbacMiddleware to enforce path-based rules.
    /// </summary>
    public static string? MinimumRoleFor(string method, string path)
    {
        // Connector / pairing paths are service-to-service — no user role.
        if (path.StartsWith("/api/connector/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/sites/pair", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/pairing-codes", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/jobs/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/jobs", StringComparison.OrdinalIgnoreCase))
            return null;

        // Public endpoints
        if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/auth/oidc/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/billing/webhook", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/v1/insight/tools", StringComparison.OrdinalIgnoreCase))
            return null;

        // Admin-only paths
        if (path.StartsWith("/api/admin/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/users", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/tenants", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/firms", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/billing/subscription", StringComparison.OrdinalIgnoreCase))
            return Admin;

        // FirmAdmin only: right-to-erasure (user data redaction)
        if (path.StartsWith("/api/compliance/users/", StringComparison.OrdinalIgnoreCase))
            return FirmAdmin;

        // Admin+: other compliance endpoints (data export)
        if (path.StartsWith("/api/compliance/", StringComparison.OrdinalIgnoreCase))
            return Admin;

        // Monitoring — Approver+
        if (path.StartsWith("/api/monitor/", StringComparison.OrdinalIgnoreCase))
            return Approver;

        // Approver-only: approve / reject proposals
        if (path.StartsWith("/api/act/proposals/", StringComparison.OrdinalIgnoreCase) &&
            (path.EndsWith("/approve", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/reject", StringComparison.OrdinalIgnoreCase)))
            return Approver;

        // Approver: write audit log
        if (path.StartsWith("/api/act/write-audit", StringComparison.OrdinalIgnoreCase))
            return Approver;

        // Preparer+: Act proposals (read or write) and other Act endpoints
        if (path.StartsWith("/api/act/", StringComparison.OrdinalIgnoreCase))
            return Preparer;

        // Reader+: Insight + mobile app config
        if (path.StartsWith("/api/insight/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/insight/dashboard", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/mobile/", StringComparison.OrdinalIgnoreCase))
            return Reader;

        // Sites list — Reader+
        if (path.Equals("/api/sites", StringComparison.OrdinalIgnoreCase))
            return Reader;

        return null; // all other paths are public
    }
}
