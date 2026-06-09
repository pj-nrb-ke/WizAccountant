using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

public sealed class AuthService(AppDbContext db, WizTokenService tokens)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        if (user is null) return null;

        // Support both hashed (BCrypt $2) and legacy plain-text passwords during migration window.
        var passwordValid = user.Password.StartsWith("$2", StringComparison.Ordinal)
            ? BCrypt.Net.BCrypt.Verify(request.Password, user.Password)
            : user.Password == request.Password;

        if (!passwordValid) return null;

        // Opportunistically upgrade plain-text password to hash on successful login.
        if (!user.Password.StartsWith("$2", StringComparison.Ordinal))
            await UpgradePasswordHashAsync(user.UserId, request.Password, ct);

        return new LoginResponse
        {
            Token = tokens.GenerateToken(user.UserId, user.TenantId, user.Email, user.Role),
            TenantId = user.TenantId,
            UserId = user.UserId,
            DisplayName = user.DisplayName,
            Role = user.Role
        };
    }

    private async Task UpgradePasswordHashAsync(Guid userId, string plainPassword, CancellationToken ct)
    {
        var mutable = await db.Users.FindAsync([userId], ct);
        if (mutable is null) return;
        mutable.Password = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<TenantDto>> ListTenantsAsync(CancellationToken ct) =>
        await db.Tenants.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantDto { TenantId = t.TenantId, Name = t.Name })
            .ToListAsync(ct);

    public async Task<List<UserDto>> ListUsersAsync(string? tenantId, CancellationToken ct)
    {
        var q = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tenantId))
            q = q.Where(u => u.TenantId == tenantId);
        return await q.OrderBy(u => u.Email)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                TenantId = u.TenantId,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role
            })
            .ToListAsync(ct);
    }
}
