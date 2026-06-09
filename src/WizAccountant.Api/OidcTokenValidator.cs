using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace WizAccountant.Api;

/// <summary>
/// Phase 4 Block 4 (Task #18) — validates an external OIDC id_token against the
/// provider's JWKS endpoint and returns extracted claims.
///
/// Supports any OIDC-compliant provider (AzureAD, Google, Okta, etc.) configured
/// in OidcSettings:Providers. JWKS keys are cached in IMemoryCache with a 1-hour TTL.
/// Thread-safe: concurrent requests for the same provider share one refresh.
/// </summary>
public interface IOidcTokenValidator
{
    Task<OidcClaims?> ValidateAsync(string provider, string idToken, CancellationToken ct = default);
}

public sealed class OidcTokenValidator(
    IOptions<OidcSettings> settings,
    IHttpClientFactory httpFactory,
    IMemoryCache memoryCache,
    ILogger<OidcTokenValidator> logger)
    : IOidcTokenValidator
{
    private static readonly TimeSpan JwksCacheTtl = TimeSpan.FromHours(1);

    public async Task<OidcClaims?> ValidateAsync(string provider, string idToken, CancellationToken ct = default)
    {
        var config = settings.Value.GetProvider(provider);
        if (config is null || !config.IsValid())
        {
            logger.LogWarning("OIDC provider '{Provider}' is not configured or invalid.", provider);
            return null;
        }

        try
        {
            var signingKeys = await GetSigningKeysAsync(provider, config, ct);
            var handler = new JwtSecurityTokenHandler { MaximumTokenSizeInBytes = 1024 * 64 };

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateIssuer = true,
                ValidIssuer = config.Issuer,
                ValidateAudience = true,
                ValidAudience = config.ClientId,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
                NameClaimType = "name",
                RoleClaimType = "roles",
            };

            var principal = handler.ValidateToken(idToken, validationParams, out _);
            return ExtractClaims(provider, principal);
        }
        catch (SecurityTokenException ex)
        {
            logger.LogWarning("OIDC token validation failed for provider '{Provider}': {Message}", provider, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error validating OIDC token for provider '{Provider}'.", provider);
            return null;
        }
    }

    private async Task<IList<SecurityKey>> GetSigningKeysAsync(
        string provider, OidcProviderConfig config, CancellationToken ct)
    {
        var cacheKey = $"oidc_jwks_{provider}";
        if (memoryCache.TryGetValue(cacheKey, out IList<SecurityKey>? cached) && cached is not null)
            return cached;

        var http = httpFactory.CreateClient("OidcJwks");
        var jwksJson = await http.GetStringAsync(config.JwksUri, ct);
        var keys = new JsonWebKeySet(jwksJson).GetSigningKeys();

        memoryCache.Set(cacheKey, keys, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = JwksCacheTtl,
            Size = 1,
        });
        logger.LogInformation("Refreshed JWKS for '{Provider}' via {Uri} — {Count} key(s).",
            provider, config.JwksUri, keys.Count);
        return keys;
    }

    private static OidcClaims? ExtractClaims(string provider, ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub)) return null;

        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? principal.FindFirstValue("preferred_username")
                    ?? string.Empty;

        var name = principal.FindFirstValue("name")
                   ?? principal.FindFirstValue(ClaimTypes.Name)
                   ?? principal.FindFirstValue("given_name")
                   ?? email;

        return new OidcClaims(provider, sub, email, name);
    }
}
