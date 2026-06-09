using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WizAccountant.Api;

namespace WizAccountant.Integration.Tests;

/// <summary>
/// WebApplicationFactory for integration tests.
/// Replaces the real SQLite DB with an in-memory one and sets a test JWT secret.
/// Mark tests with [Trait("Category","Integration")] so they can be run selectively:
///   dotnet test --filter "Category=Integration"
/// </summary>
public sealed class WizApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // Replace with a unique in-memory SQLite (file-per-test)
            var dbPath = Path.Combine(Path.GetTempPath(), $"wiz-int-{Guid.NewGuid():N}.db");
            services.AddDbContext<AppDbContext>(opt =>
                opt.UseSqlite($"Data Source={dbPath}"));
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = new string('I', 32),
                ["Jwt:Issuer"] = "WizIntegrationTest",
            });
        });
    }
}
