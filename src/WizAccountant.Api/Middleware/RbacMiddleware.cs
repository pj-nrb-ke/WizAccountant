using System.Security.Claims;

namespace WizAccountant.Api.Middleware;

/// <summary>
/// RBAC v2 middleware — enforces minimum role requirements per API path.
/// Runs after authentication so ClaimsPrincipal is available.
/// Role hierarchy enforced via WizRoles.HasAtLeast.
/// </summary>
public sealed class RbacMiddleware(RequestDelegate next, ILogger<RbacMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        var requiredRole = WizRoles.MinimumRoleFor(method, path);

        if (requiredRole is null)
        {
            // Public endpoint — no role check
            await next(context);
            return;
        }

        // Extract role from JWT claims (set by AddAuthentication/AddJwtBearer)
        var userRole = context.User.FindFirstValue("role")
                    ?? context.User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userRole) || !context.User.Identity?.IsAuthenticated == true)
        {
            logger.LogWarning("RBAC: unauthenticated request to protected path {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required." });
            return;
        }

        if (!WizRoles.HasAtLeast(userRole, requiredRole))
        {
            logger.LogWarning(
                "RBAC: role {UserRole} insufficient for {Path} (requires {Required})",
                userRole, path, requiredRole);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"Insufficient role. Required: {requiredRole}, current: {userRole}."
            });
            return;
        }

        await next(context);
    }
}
