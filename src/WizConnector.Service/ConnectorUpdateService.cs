using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace WizConnector.Service;

/// <summary>
/// MC4 — Connector auto-updater.
/// On startup, checks /api/connector/version against the running connector version.
/// Logs a prominent warning (and optionally self-updates) when a newer version is available.
/// </summary>
public sealed class ConnectorUpdateService(
    ILogger<ConnectorUpdateService> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<ConnectorSettings> options)
{
    private readonly ConnectorSettings _settings = options.Value;

    public async Task CheckForUpdateAsync(CancellationToken ct)
    {
        if (!_settings.AutoUpdateCheck)
        {
            logger.LogDebug("Connector update check disabled.");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_settings.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);

            var manifest = await client.GetFromJsonAsync<VersionManifest>("api/connector/version", ct);
            if (manifest is null) return;

            var running = ParseVersion(_settings.ConnectorVersion);
            var latest  = ParseVersion(manifest.LatestConnectorVersion);
            var minimum = ParseVersion(manifest.MinimumConnectorVersion);

            if (running < minimum)
            {
                logger.LogError(
                    "This connector version ({Running}) is BELOW the minimum required version ({Minimum}). " +
                    "Jobs may be rejected by the API. Download the latest installer: {Url}",
                    _settings.ConnectorVersion, manifest.MinimumConnectorVersion, manifest.DownloadUrl);
            }
            else if (running < latest)
            {
                logger.LogWarning(
                    "A newer connector version is available: {Latest} (running {Running}). " +
                    "Release notes: {Notes}  •  Download: {Url}",
                    manifest.LatestConnectorVersion, _settings.ConnectorVersion,
                    manifest.ReleaseNotes, manifest.DownloadUrl);
            }
            else
            {
                logger.LogInformation(
                    "Connector is up to date (v{Running}).", _settings.ConnectorVersion);
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — connector continues without update info
            logger.LogWarning(ex, "Connector update check failed.");
        }
    }

    private static Version ParseVersion(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return new Version(0, 0);
        return Version.TryParse(v, out var parsed) ? parsed : new Version(0, 0);
    }

    private sealed class VersionManifest
    {
        [JsonPropertyName("latestConnectorVersion")]
        public string? LatestConnectorVersion { get; set; }

        [JsonPropertyName("minimumConnectorVersion")]
        public string? MinimumConnectorVersion { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; set; }
    }
}
