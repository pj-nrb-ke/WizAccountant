using System.Net.Http.Json;
using System.Text.Json;
using WizAccountant.Contracts;

namespace WizConnector.Tray;

internal sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private string _baseUrl = "";

    public string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value.Trim().TrimEnd('/');
    }

    private Uri ApiUri(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
            throw new InvalidOperationException("API URL is not set.");
        var path = relativePath.TrimStart('/');
        return new Uri($"{_baseUrl}/{path}");
    }

    public async Task<PairSiteResponse?> PairAsync(string pairingCode, string deviceId, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(ApiUri("api/sites/pair"), new PairSiteRequest
        {
            PairingCode = pairingCode.Trim(),
            DeviceId = deviceId,
            ConnectorVersion = "0.1.0-tray"
        }, ct);

        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<PairSiteResponse>(json, JsonOptions);
    }

    public async Task<SiteDto?> GetSiteAsync(Guid siteId, CancellationToken ct = default)
    {
        var response = await http.GetAsync(ApiUri("api/sites"), ct);
        if (!response.IsSuccessStatusCode) return null;
        var sites = await response.Content.ReadFromJsonAsync<List<SiteDto>>(JsonOptions, ct);
        return sites?.FirstOrDefault(s => s.SiteId == siteId);
    }
}
