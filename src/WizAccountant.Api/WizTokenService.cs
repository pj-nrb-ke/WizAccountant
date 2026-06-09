using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace WizAccountant.Api;

/// <summary>
/// Reusable service for JWT generation and validation.
/// Token contains: sub (userId), tenant, role, email.
/// Algorithm: HMAC-SHA256. Expiry: configurable (default 12 hours).
/// </summary>
public sealed class WizTokenService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly int _expiryHours;

    public WizTokenService(IConfiguration config)
    {
        _secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured. Add it to appsettings or user-secrets.");
        _issuer = config["Jwt:Issuer"] ?? "WizAccountant";
        _expiryHours = int.TryParse(config["Jwt:ExpiryHours"], out var h) ? h : 12;
    }

    public string GenerateToken(Guid userId, string tenantId, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenant", tenantId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_expiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool TryValidateToken(string token, out Guid userId, out string tenantId, out string role)
    {
        userId = Guid.Empty;
        tenantId = string.Empty;
        role = string.Empty;

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            // Disable default claim-type remapping so "sub" stays as "sub".
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _issuer,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            }, out _);

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (!Guid.TryParse(sub, out userId)) return false;

            tenantId = principal.FindFirstValue("tenant") ?? string.Empty;
            role = principal.FindFirstValue("role") ?? string.Empty;
            return !string.IsNullOrEmpty(tenantId);
        }
        catch
        {
            return false;
        }
    }

    public TokenValidationParameters GetValidationParameters()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _issuer,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    }
}
