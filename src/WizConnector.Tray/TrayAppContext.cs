using System.Diagnostics;
using WizAccountant.Contracts;

namespace WizConnector.Tray;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly HttpClient _http = new();
    private readonly ApiClient _api;
    private StatusForm? _statusForm;

    public TrayAppContext()
    {
        var settings = TraySettings.Load();
        _api = new ApiClient(_http);
        UpdateApiUrl(settings.ApiBaseUrl);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WizConnector",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (_, _) => ShowStatus();
    }

    public ApiClient Api => _api;

    public void UpdateApiUrl(string url) => _api.BaseUrl = url;

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Status…", null, (_, _) => ShowStatus());
        menu.Items.Add("Pair with code…", null, (_, _) => ShowPairing());
        menu.Items.Add("Open Sage Setup", null, (_, _) => OpenSageSetup());
        menu.Items.Add("Allow cloud posts (1 hour)", null, (_, _) => GrantWriteConsent());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open config folder", null, (_, _) => OpenConfigFolder());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    public void ShowStatus()
    {
        if (_statusForm is { IsDisposed: false })
        {
            _statusForm.BringToFront();
            _statusForm.Show();
            return;
        }

        _statusForm = new StatusForm(this);
        _statusForm.Show();
    }

    public void ShowPairing()
    {
        using var form = new PairingForm(this);
        form.ShowDialog();
        UpdateTrayTooltip();
        _statusForm?.RefreshStatusAsync();
    }

    public async Task<bool> PairWithCodeAsync(string pairingCode)
    {
        if (string.IsNullOrWhiteSpace(pairingCode)) return false;

        var paired = await _api.PairAsync(pairingCode, Environment.MachineName);
        if (paired is null) return false;

        ConnectorStateStore.Save(new ConnectorStateDto
        {
            SiteId = paired.SiteId,
            TenantId = paired.TenantId,
            SiteName = paired.SiteName
        });
        UpdateTrayTooltip();
        return true;
    }

    public void OpenSageSetup()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "WizConnector.Setup.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "WizConnector.Setup", "bin", "Release", "net8.0-windows", "WizConnector.Setup.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "WizConnector.Setup", "bin", "Debug", "net8.0-windows", "WizConnector.Setup.exe"))
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        MessageBox.Show(
            "WizConnector.Setup.exe not found next to Tray.\r\nBuild the Setup project or run from the same output folder.",
            "WizConnector",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    public void OpenConfigFolder()
    {
        Directory.CreateDirectory(ConnectorPaths.ConfigFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", ConnectorPaths.ConfigFolder) { UseShellExecute = true });
    }

    private static void GrantWriteConsent()
    {
        WriteConsentHelper.Grant(TimeSpan.FromHours(1));
        MessageBox.Show(
            "Cloud write jobs may run for the next hour.\r\nAlso set Connector:WritesEnabled=true on the service.",
            "WizConnector",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void UpdateTrayTooltip()
    {
        var state = ConnectorStateStore.Load();
        _tray.Text = state is null
            ? "WizConnector — not paired"
            : $"WizConnector — {state.SiteName}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _http.Dispose();
            _statusForm?.Dispose();
        }

        base.Dispose(disposing);
    }
}
