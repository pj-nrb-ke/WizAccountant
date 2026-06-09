using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

/// <summary>
/// Phase 4 Block 4 (Task #18) — SSO login via external OIDC providers.
///
/// Flow:
///   1. Validate the external id_token (signature + claims) via IOidcTokenValidator
///   2. Look up ExternalIdentityRecord by (Provider, Subject)
///   3. If found → locate the linked WizAccountant user
///   4. If not found → match by email address, or create a new user
///   5. Create / update ExternalIdentityRecord link
///   6. Issue a WizAccountant JWT (same as username/password login)
/// </summary>
public sealed class OidcAuthService(
    AppDbContext db,
    IOidcTokenValidator tokenValidator,
    WizTokenService tokens,
    ILogger<OidcAuthService> logger)
{
    public async Task<LoginResponse?> LoginAsync(OidcLoginRequest request, CancellationToken ct)
    {
        // 1. Validate the external id_token
        var claims = await tokenValidator.ValidateAsync(request.Provider, request.IdToken, ct);
        if (claims is null)
        {
            logger.LogWarning("OIDC login rejected — invalid token for provider '{Provider}'.", request.Provider);
            return null;
        }

        // 2. Look up existing external identity link
        var identity = await db.ExternalIdentities.AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.Provider == claims.Provider &&
                e.Subject == claims.Subject, ct);

        UserRecord? user = null;

        if (identity is not null)
        {
            // 3a. Found existing link → load the user
            user = await db.Users.FindAsync([identity.UserId], ct);
        }

        if (user is null && !string.IsNullOrWhiteSpace(claims.Email))
        {
            // 3b. No link or user missing — try to find user by email
            user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == claims.Email.ToLowerInvariant(), ct);
        }

        if (user is null)
        {
            // 4. Create a new user (auto-provision)
            // Tenant assignment: use email domain → find matching tenant, else use first available
            var tenantId = await ResolveDefaultTenantAsync(claims.Email, ct)
                           ?? "default";

            user = new UserRecord
            {
                UserId = Guid.NewGuid(),
                TenantId = tenantId,
                Email = claims.Email.ToLowerInvariant(),
                DisplayName = claims.DisplayName,
                Password = string.Empty, // SSO users have no password
                Role = "Reader",
            };
            db.Users.Add(user);
            logger.LogInformation("Auto-provisioned SSO user {Email} from provider {Provider}.",
                claims.Email, claims.Provider);
        }

        // 5. Create or update ExternalIdentityRecord
        var now = DateTimeOffset.UtcNow;
        if (identity is null)
        {
            db.ExternalIdentities.Add(new ExternalIdentityRecord
            {
                ExternalIdentityId = Guid.NewGuid(),
                Provider = claims.Provider,
                Subject = claims.Subject,
                UserId = user.UserId,
                ProviderEmail = claims.Email,
                LinkedAtUtc = now,
                LastLoginAtUtc = now,
            });
        }
        else
        {
            // Update last-login timestamp
            var tracked = await db.ExternalIdentities.FindAsync([identity.ExternalIdentityId], ct);
            if (tracked is not null) tracked.LastLoginAtUtc = now;
        }

        await db.SaveChangesAsync(ct);

        // 6. Resolve practice mode + firm for LoginResponse
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == user.TenantId, ct);
        var firmId = tenant?.FirmId;
        var practiceMode = false;
        if (firmId is not null)
        {
            var firm = await db.Firms.AsNoTracking()
                .FirstOrDefaultAsync(f => f.FirmId == firmId, ct);
            practiceMode = firm?.IsPracticeMode == true;
        }

        var token = tokens.GenerateToken(user.UserId, user.TenantId, user.Email, user.Role);
        return new LoginResponse
        {
            Token = token,
            TenantId = user.TenantId,
            UserId = user.UserId,
            DisplayName = user.DisplayName,
            Role = user.Role,
            PracticeMode = practiceMode,
            FirmId = firmId,
        };
    }

    private async Task<string?> ResolveDefaultTenantAsync(string email, CancellationToken ct)
    {
        // Simple heuristic: match email domain to tenant name (case-insensitive contains)
        if (string.IsNullOrWhiteSpace(email)) return null;
        var domain = email.Contains('@') ? email.Split('@')[1].ToLowerInvariant() : null;
        if (domain is null) return null;

        var allTenants = await db.Tenants.AsNoTracking().ToListAsync(ct);
        var match = allTenants.FirstOrDefault(t =>
            t.Name.Contains(domain.Split('.')[0], StringComparison.OrdinalIgnoreCase));
        return match?.TenantId;
    }
}
