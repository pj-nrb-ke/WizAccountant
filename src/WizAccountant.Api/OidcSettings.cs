namespace WizAccountant.Api;

/// <summary>
/// OIDC provider configuration — loaded from appsettings "Oidc:Providers".
/// Supports Azure AD and Google out of the box; any OIDC-compliant provider works.
///
/// Example appsettings.json:
/// "Oidc": {
///   "Providers": [
///     {
///       "Provider": "AzureAD",
///       "Issuer":   "https://login.microsoftonline.com/{tenantId}/v2.0",
///       "ClientId": "YOUR_AZURE_AD_CLIENT_ID",
///       "JwksUri":  "https://login.microsoftonline.com/{tenantId}/discovery/v2.0/keys"
///     },
///     {
///       "Provider": "Google",
///       "Issuer":   "https://accounts.google.com",
///       "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com",
///       "JwksUri":  "https://www.googleapis.com/oauth2/v3/certs"
///     }
///   ]
/// }
/// </summary>
public sealed class OidcSettings
{
    public List<OidcProviderConfig> Providers { get; set; } = [];

    public OidcProviderConfig? GetProvider(string providerName) =>
        Providers.FirstOrDefault(p => p.Provider.Equals(providerName, StringComparison.OrdinalIgnoreCase));
}

public sealed class OidcProviderConfig
{
    /// <summary>Provider name: "AzureAD" or "Google" (or any custom label).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>JWT issuer claim value (iss). Must match the id_token's iss field exactly.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>OAuth2 client ID — used as the expected audience (aud) in the id_token.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>JWKS endpoint URL for fetching provider's public signing keys.</summary>
    public string JwksUri { get; set; } = string.Empty;

    /// <summary>Optional: default role to assign newly created SSO users.</summary>
    public string DefaultRole { get; set; } = "Reader";

    /// <summary>Optional: default tenant ID to place new SSO users in (if not found by email domain).</summary>
    public string? DefaultTenantId { get; set; }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Provider) &&
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(JwksUri);
}

/// <summary>Claims extracted from a validated external OIDC id_token.</summary>
public sealed record OidcClaims(
    string Provider,
    string Subject,       // "sub" claim — stable external user ID
    string Email,
    string DisplayName
);
