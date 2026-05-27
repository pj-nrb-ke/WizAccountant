using Microsoft.EntityFrameworkCore;

namespace WizAccountant.Api;

public static class DbSeed
{
    public static async Task EnsurePhase1SeedAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS Tenants (
                TenantId TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Users (
                UserId TEXT NOT NULL PRIMARY KEY,
                TenantId TEXT NOT NULL,
                Email TEXT NOT NULL,
                Password TEXT NOT NULL,
                DisplayName TEXT NOT NULL
            );
            """);

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
                Password = "pilot",
                DisplayName = "Pilot Admin"
            });
            await db.SaveChangesAsync();
        }
    }
}
