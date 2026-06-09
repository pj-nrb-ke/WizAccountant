using Microsoft.Extensions.Options;
using Pastel.Evolution;

namespace WizConnector.Service.Sage;

/// <summary>
/// Thread-safe Evolution SDK connection scope (one company DB per connector instance).
/// </summary>
public sealed class SageSession
{
    private readonly SageSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SageSession(IOptions<SageSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<T> RunAsync<T>(Func<T> action, CancellationToken ct)
        => RunAsync(action, companyAlias: null, ct);

    /// <summary>MC1 — run action against a specific company (null = default).</summary>
    public async Task<T> RunAsync<T>(Func<T> action, string? companyAlias, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    Connect(_settings.ResolveCompanyConnectionString(companyAlias));
                    return action();
                }
                catch (Exception ex) when (attempt == 0 && IsConnectionError(ex))
                {
                    // Evolution SQL connection can drop between jobs — retry once.
                }
            }

            throw new InvalidOperationException("Sage connection failed after retry.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>MC1 — list all configured company aliases.</summary>
    public IReadOnlyList<string> GetCompanyAliases()
    {
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.CompanyConnectionString))
            result.Add("(default)");
        result.AddRange(_settings.Companies.Keys);
        return result;
    }

    /// <summary>Same sequence as WizConnector.Setup "Test Sage connection".</summary>
    private void Connect(string companyConnectionString)
    {
        if (string.IsNullOrWhiteSpace(_settings.CommonConnectionString))
            throw new InvalidOperationException(
                "Sage common database is not configured. In WizPilot click Open Sage setup, select the common database (e.g. SageCommon11), Test Sage connection, then Save.");

        if (string.IsNullOrWhiteSpace(companyConnectionString))
            throw new InvalidOperationException(
                "Sage company database is not configured. Open Sage setup and save your company database.");

        if (string.IsNullOrWhiteSpace(_settings.LicenseSerial) || string.IsNullOrWhiteSpace(_settings.LicenseKey))
            throw new InvalidOperationException(
                "Sage SDK licence is not configured. Open Sage setup and enter licence serial and key.");

        DatabaseContext.CreateCommonDBConnection(_settings.CommonConnectionString);
        DatabaseContext.SetLicense(_settings.LicenseSerial, _settings.LicenseKey);
        DatabaseContext.CreateConnection(companyConnectionString);

        if (_settings.BranchId > 0)
            DatabaseContext.SetBranchContext(_settings.BranchId);

        if (!string.IsNullOrWhiteSpace(_settings.AgentUser))
        {
            if (!string.IsNullOrWhiteSpace(_settings.AgentPassword))
            {
                if (!Agent.Authenticate(_settings.AgentUser, _settings.AgentPassword))
                    throw new InvalidOperationException(
                        $"Sage agent authentication failed for user '{_settings.AgentUser}'. Check agent password in Sage setup.");
            }

            DatabaseContext.CurrentAgent = new Agent(_settings.AgentUser);
        }
    }

    private static bool IsConnectionError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("ADP_ConnectionRequired", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
