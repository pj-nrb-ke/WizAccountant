using System.Text;
using Microsoft.EntityFrameworkCore;
using WizAccountant.Contracts;

namespace WizAccountant.Api;

public sealed class AuthService(AppDbContext db)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (user is null || user.Password != request.Password)
            return null;

        return new LoginResponse
        {
            Token = EncodeToken(user.TenantId, user.UserId),
            TenantId = user.TenantId,
            UserId = user.UserId,
            DisplayName = user.DisplayName,
            Role = user.Role
        };
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

    public static string EncodeToken(string tenantId, Guid userId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"wiz:{tenantId}:{userId}"));

    public static bool TryDecodeToken(string token, out string tenantId, out Guid userId)
    {
        tenantId = string.Empty;
        userId = Guid.Empty;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(token)).Split(':');
            if (parts.Length != 3 || parts[0] != "wiz") return false;
            tenantId = parts[1];
            return Guid.TryParse(parts[2], out userId);
        }
        catch
        {
            return false;
        }
    }
}
