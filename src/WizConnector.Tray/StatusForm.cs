using WizAccountant.Contracts;

namespace WizConnector.Tray;

internal sealed class StatusForm : Form
{
    private readonly TrayAppContext _app;
    private readonly Label _deviceId = new() { AutoSize = true };
    private readonly Label _siteInfo = new() { AutoSize = true, MaximumSize = new Size(420, 0) };
    private readonly Label _online = new() { AutoSize = true };
    private readonly TextBox _apiUrl;
    private readonly System.Windows.Forms.Timer _timer;

    public StatusForm(TrayAppContext app)
    {
        _app = app;
        Text = "WizConnector Status";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 280);

        var settings = TraySettings.Load();
        _apiUrl = new TextBox { Width = 300, Text = settings.ApiBaseUrl };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, 0, "Device ID", _deviceId);
        AddRow(layout, 1, "API URL", _apiUrl);
        AddRow(layout, 2, "Site", _siteInfo);
        AddRow(layout, 3, "Cloud status", _online);

        var btnPair = new Button { Text = "Pair with code…", Width = 140 };
        btnPair.Click += (_, _) => _app.ShowPairing();
        var btnSetup = new Button { Text = "Open Sage Setup", Width = 140 };
        btnSetup.Click += (_, _) => _app.OpenSageSetup();
        var btnSaveUrl = new Button { Text = "Save URL", Width = 90 };
        btnSaveUrl.Click += (_, _) => SaveApiUrl(showSaved: true);
        var btnRefresh = new Button { Text = "Refresh", Width = 90 };
        btnRefresh.Click += async (_, _) => await RefreshStatusAsync();

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Padding = new Padding(12), AutoSize = true };
        buttons.Controls.Add(btnPair);
        buttons.Controls.Add(btnSetup);
        buttons.Controls.Add(btnSaveUrl);
        buttons.Controls.Add(btnRefresh);

        var hint = new Label
        {
            Text = "Start WizConnector.Service on this PC to show Online in the cloud.",
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ForeColor = Color.Gray,
            Padding = new Padding(12, 0, 12, 8)
        };

        Controls.Add(layout);
        Controls.Add(hint);
        Controls.Add(buttons);

        _online.Font = new Font(Font, FontStyle.Bold);
        _deviceId.Text = Environment.MachineName;
        _timer = new System.Windows.Forms.Timer { Interval = 15000 };
        _timer.Tick += async (_, _) => await RefreshStatusAsync();
        Shown += async (_, _) =>
        {
            _timer.Start();
            await RefreshStatusAsync();
        };
        FormClosed += (_, _) => _timer.Stop();
    }

    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Margin = new Padding(0, 4, 0, 8);
        panel.Controls.Add(control, 1, row);
    }

    private void SaveApiUrl(bool showSaved = false)
    {
        var url = _apiUrl.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("Enter the cloud API URL (e.g. https://app.ascendbooks.biz).", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var settings = TraySettings.Load();
        settings.ApiBaseUrl = url;
        settings.Save();
        _app.UpdateApiUrl(url);
        if (showSaved)
            _online.Text = "API URL saved.";
    }

    public async Task RefreshStatusAsync()
    {
        SaveApiUrl();

        var state = ConnectorStateStore.Load();
        if (state is null)
        {
            _siteInfo.Text = "Not paired — use Pair with code.";
            _online.Text = "—";
            _online.ForeColor = Color.Gray;
            return;
        }

        _siteInfo.Text = $"{state.SiteName}\r\n{state.SiteId}";
        try
        {
            var site = await _app.Api.GetSiteAsync(state.SiteId);
            if (site is null)
            {
                _online.Text = "Unknown (API error)";
                _online.ForeColor = Color.DarkOrange;
                return;
            }

            _online.Text = site.IsOnline ? "Online" : "Offline";
            _online.ForeColor = site.IsOnline ? Color.DarkGreen : Color.DarkRed;
            if (site.LastSeenUtc.HasValue)
                _online.Text += $" — last seen {site.LastSeenUtc:u}";
        }
        catch (Exception ex)
        {
            _online.Text = "Cannot reach API: " + ex.Message;
            _online.ForeColor = Color.DarkRed;
        }
    }
}
