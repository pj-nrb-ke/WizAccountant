using Microsoft.EntityFrameworkCore;
using BC = BCrypt.Net.BCrypt;

namespace WizAccountant.Api;

/// <summary>
/// Handles idempotent DATA seeding only.
/// Schema creation and migrations are managed by EF Core Migrations (db.Database.MigrateAsync).
/// </summary>
public static class DbSeed
{
    /// <summary>
    /// Hashes any remaining plain-text passwords (passwords not starting with $2).
    /// Runs at startup to handle databases created before the BCrypt upgrade.
    /// </summary>
    public static async Task MigratePasswordHashesAsync(AppDbContext db)
    {
        var plainTextUsers = await db.Users
            .Where(u => !u.Password.StartsWith("$2"))
            .ToListAsync();

        foreach (var user in plainTextUsers)
            user.Password = BC.HashPassword(user.Password, workFactor: 12);

        if (plainTextUsers.Count > 0)
            await db.SaveChangesAsync();
    }

    /// <summary>Seeds all required reference data across all phases.</summary>
    public static async Task EnsurePhase1SeedAsync(AppDbContext db)
    {
        if (!await db.Tenants.AnyAsync())
        {
            db.Tenants.Add(new TenantRecord { TenantId = "pilot-tenant", Name = "Pilot Tenant" });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TenantId = "pilot-tenant",
                Email = "admin@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Admin",
                Role = "Admin"
            });
            await db.SaveChangesAsync();
        }

        await EnsurePhase3UsersAsync(db);
        await EnsurePhase4UsersAsync(db);
        await EnsurePhase4FirmSeedAsync(db);
        await EnsurePhase4SsoSeedAsync(db);
    }

    public static async Task EnsurePhase3UsersAsync(AppDbContext db)
    {
        var admin = await db.Users.FindAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        if (admin is not null && admin.Role != "Admin")
        {
            admin.Role = "Admin";
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Email == "approver@pilot.local"))
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TenantId = "pilot-tenant",
                Email = "approver@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Approver",
                Role = "Approver"
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Email == "preparer@pilot.local"))
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                TenantId = "pilot-tenant",
                Email = "preparer@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Preparer",
                Role = "Preparer"
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Seeds Phase 4 users: reader + firmadmin.</summary>
    public static async Task EnsurePhase4UsersAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync(u => u.Email == "reader@pilot.local"))
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                TenantId = "pilot-tenant",
                Email = "reader@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Reader",
                Role = WizRoles.Reader
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Email == "firmadmin@pilot.local"))
        {
            db.Users.Add(new UserRecord
            {
                UserId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantId = "pilot-tenant",
                Email = "firmadmin@pilot.local",
                Password = BC.HashPassword("pilot", workFactor: 12),
                DisplayName = "Pilot Firm Admin",
                Role = WizRoles.FirmAdmin
            });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Seeds pilot-firm and assigns pilot-tenant to it.</summary>
    public static async Task EnsurePhase4FirmSeedAsync(AppDbContext db)
    {
        if (!await db.Firms.AnyAsync())
        {
            db.Firms.Add(new FirmRecord
            {
                FirmId = "pilot-firm",
                Name = "Pilot Firm",
                IsPracticeMode = false,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var pilotTenant = await db.Tenants.FindAsync(["pilot-tenant"]);
        if (pilotTenant is not null && pilotTenant.FirmId is null)
        {
            pilotTenant.FirmId = "pilot-firm";
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Seeds free/trialing subscription for pilot-tenant.</summary>
    public static async Task EnsurePhase4SsoSeedAsync(AppDbContext db)
    {
        if (!await db.Subscriptions.AnyAsync(s => s.TenantId == "pilot-tenant"))
        {
            db.Subscriptions.Add(new SubscriptionRecord
            {
                TenantId = "pilot-tenant",
                Plan = "pro",
                Status = "trialing",
                BillingRef = null,
                CurrentPeriodEnd = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
    }
}
