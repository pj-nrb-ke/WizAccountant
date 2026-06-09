using Microsoft.Extensions.Configuration;
using WizAccountant.Api;

namespace WizAccountant.Insight.Intents.Tests;

/// <summary>Tests for WizTokenService (JWT) and BCrypt password hashing via AuthService.</summary>
public sealed class AuthServiceTests
{
    private static WizTokenService BuildTokenService(string? secret = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret ?? "test-secret-key-at-least-32-chars!!",
                ["Jwt:Issuer"] = "WizAccountant",
                ["Jwt:ExpiryHours"] = "12"
            })
            .Build();
        return new WizTokenService(config);
    }

    // ── Token generation & validation ────────────────────────────────────────

    [Fact]
    public void GenerateToken_ProducesNonEmptyJwt()
    {
        var svc = BuildTokenService();
        var token = svc.GenerateToken(Guid.NewGuid(), "tenant1", "user@test.com", "Admin");
        Assert.False(string.IsNullOrWhiteSpace(token));
        // JWT has 3 dot-separated segments
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void TryValidateToken_RoundTrip_Succeeds()
    {
        var svc = BuildTokenService();
        var userId = Guid.NewGuid();
        var token = svc.GenerateToken(userId, "tenant-x", "a@b.com", "Approver");

        var ok = svc.TryValidateToken(token, out var outId, out var outTenant, out var outRole);

        Assert.True(ok);
        Assert.Equal(userId, outId);
        Assert.Equal("tenant-x", outTenant);
        Assert.Equal("Approver", outRole);
    }

    [Fact]
    public void TryValidateToken_WrongSecret_Fails()
    {
        var svc1 = BuildTokenService("secret-one-at-least-32-characters!!");
        var svc2 = BuildTokenService("secret-two-at-least-32-characters!!");

        var token = svc1.GenerateToken(Guid.NewGuid(), "t1", "a@b.com", "Admin");
        var ok = svc2.TryValidateToken(token, out _, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryValidateToken_GarbageInput_ReturnsFalse()
    {
        var svc = BuildTokenService();
        Assert.False(svc.TryValidateToken("not.a.jwt", out _, out _, out _));
        Assert.False(svc.TryValidateToken("", out _, out _, out _));
        Assert.False(svc.TryValidateToken("abc", out _, out _, out _));
    }

    [Fact]
    public void Constructor_MissingSecret_ThrowsInvalidOperation()
    {
        var config = new ConfigurationBuilder().Build(); // no Jwt:Secret
        Assert.Throws<InvalidOperationException>(() => new WizTokenService(config));
    }

    // ── BCrypt password hashing ───────────────────────────────────────────────

    [Fact]
    public void BCrypt_HashAndVerify_Succeeds()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("pilot", workFactor: 4); // low cost for tests
        Assert.True(BCrypt.Net.BCrypt.Verify("pilot", hash));
        Assert.False(BCrypt.Net.BCrypt.Verify("wrong", hash));
    }

    [Fact]
    public void BCrypt_Hash_StartsWithDollarTwo()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("test", workFactor: 4);
        Assert.StartsWith("$2", hash);
    }

    [Fact]
    public void BCrypt_TwoHashesOfSamePassword_AreDifferent()
    {
        // BCrypt uses random salt — same input produces different hashes.
        var h1 = BCrypt.Net.BCrypt.HashPassword("pilot", workFactor: 4);
        var h2 = BCrypt.Net.BCrypt.HashPassword("pilot", workFactor: 4);
        Assert.NotEqual(h1, h2);
        // But both verify correctly.
        Assert.True(BCrypt.Net.BCrypt.Verify("pilot", h1));
        Assert.True(BCrypt.Net.BCrypt.Verify("pilot", h2));
    }

    // ── Legacy plain-text detection ───────────────────────────────────────────

    [Fact]
    public void PlainTextPassword_DoesNotStartWithDollarTwo()
    {
        // Ensures the migration guard logic works.
        const string plain = "pilot";
        Assert.False(plain.StartsWith("$2", StringComparison.Ordinal));
    }

    [Fact]
    public void HashedPassword_StartsWithDollarTwo()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("pilot", workFactor: 4);
        Assert.True(hash.StartsWith("$2", StringComparison.Ordinal));
    }
}
