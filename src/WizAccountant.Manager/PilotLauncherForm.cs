namespace WizAccountant.Manager;

internal sealed class PilotLauncherForm : Form
{
    private readonly LauncherSettings _settings;
    private readonly PilotProcessLauncher? _launcher;
    private readonly TextBox _apiUrl;
    private readonly TextBox _prodUrl;
    private readonly TextBox _log;
    private readonly Label _repoLabel;

    public PilotLauncherForm()
    {
        var repo = PilotProcessLauncher.FindRepoRoot();
        _settings = LauncherSettings.Load(repo);          // repo config auto-applied
        _launcher = repo is not null ? new PilotProcessLauncher(repo) : null;

        Text = "WizAccountant Pilot";
        Width = 560;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(12)
        };
        Controls.Add(root);

        _repoLabel = new Label
        {
            AutoSize = true,
            Text = repo is not null ? $"Project: {repo}" : "Project folder not found — run from WizAccountant repo build.",
            MaximumSize = new Size(520, 0)
        };
        root.Controls.Add(_repoLabel);

        // Config source indicator
        var configPath = repo is not null ? Path.Combine(repo, "pilot.config.json") : null;
        var configExists = configPath is not null && File.Exists(configPath);
        var configLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = configExists ? Color.DarkGreen : Color.Gray,
            Text = configExists
                ? $"✓ Config loaded from pilot.config.json  (AI keeps this up to date)"
                : "pilot.config.json not found — using saved/default values",
            Margin = new Padding(0, 0, 0, 4)
        };
        root.Controls.Add(configLabel);

        var urlPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 520 };
        urlPanel.Controls.Add(Label("Connector API URL (cloud or local):"));
        _apiUrl = new TextBox { Width = 500, Text = _settings.ApiBaseUrl };
        urlPanel.Controls.Add(_apiUrl);
        urlPanel.Controls.Add(Label("Production web URL:"));
        _prodUrl = new TextBox { Width = 500, Text = _settings.ProductionUrl };
        urlPanel.Controls.Add(_prodUrl);
        var saveUrls = new Button { Text = "Save URLs", Width = 120 };
        saveUrls.Click += (_, _) => SaveUrls();
        urlPanel.Controls.Add(saveUrls);
        root.Controls.Add(urlPanel);

        root.Controls.Add(Section("Connector on this PC"));
        root.Controls.Add(ButtonRow(
            ("Start connector service", (_, _) => Run(() => _launcher!.StartConnectorService(_apiUrl.Text.Trim()))),
            ("Start system tray", (_, _) => Run(() => _launcher!.StartTray())),
            ("Start service + tray", (_, _) =>
            {
                Run(() => _launcher!.StartConnectorService(_apiUrl.Text.Trim()));
                Run(() => _launcher!.StartTray());
            })));

        root.Controls.Add(Section("Setup & local API"));
        root.Controls.Add(ButtonRow(
            ("Open Sage setup", (_, _) => Run(() => _launcher!.OpenSageSetup())),
            ("Restart local API", (_, _) => Run(() => _launcher!.StartLocalApi(PilotProcessLauncher.TryParseApiPort(_apiUrl.Text.Trim())))),
            ("Build pilot apps", (_, _) => RunBuild())));

        root.Controls.Add(Section("Open in browser — local"));
        root.Controls.Add(ButtonRow(
            ("Admin", (_, _) => OpenLocal("/admin/index.html")),
            ("Insight", (_, _) => OpenLocal("/insight/index.html")),
            ("Act", (_, _) => OpenLocal("/act/index.html"))));

        root.Controls.Add(Section("Open in browser — production"));
        root.Controls.Add(ButtonRow(
            ("Admin", (_, _) => OpenProd("/admin/index.html")),
            ("Insight", (_, _) => OpenProd("/insight/index.html")),
            ("Act", (_, _) => OpenProd("/act/index.html"))));

        root.Controls.Add(Section("Scripts (PowerShell)"));
        root.Controls.Add(ButtonRow(
            ("Start all local", (_, _) => RunScript("start-wizconnector-local.ps1")),
            ("Pilot E2E test", (_, _) => RunScript("run-pilot-e2e.ps1"))));
        root.Controls.Add(ButtonRow(
            ("Deploy to cloud", (_, _) => RunScript("deploy-ascendbooks.ps1")),
            ("QA cycle 003", (_, _) => RunScript("run-qa-cycle-003.ps1"))));

        _log = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 100,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 9f)
        };
        root.Controls.Add(_log);

        Log("Ready. Use tray icon (near clock) after starting tray — right-click for Pair with code.");
        if (_launcher is null)
            Log("ERROR: Could not find WizAccountant.slnx. Build and run WizPilot.exe from this repo.");
    }

    private static Label Section(string text) =>
        new() { Text = text, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 8, 0, 4) };

    private static Label Label(string text) => new() { Text = text, AutoSize = true };

    private static FlowLayoutPanel ButtonRow(params (string text, EventHandler click)[] buttons)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 520 };
        foreach (var (text, click) in buttons)
        {
            var b = new Button { Text = text, Width = 160, Height = 32, Margin = new Padding(0, 0, 6, 6) };
            b.Click += click;
            panel.Controls.Add(b);
        }

        return panel;
    }

    private void SaveUrls()
    {
        _settings.ApiBaseUrl = _apiUrl.Text.Trim();
        _settings.ProductionUrl = _prodUrl.Text.Trim();
        _settings.Save();
        Log("URLs saved.");
    }

    private void Run(Func<LaunchResult> action)
    {
        if (_launcher is null)
        {
            Log("Cannot run — project folder not found.");
            return;
        }

        SaveUrls();
        var r = action();
        Log(r.Message);
    }

    private void OpenLocal(string path)
    {
        var baseUrl = _apiUrl.Text.Trim().TrimEnd('/');
        Run(() => _launcher!.OpenUrl($"{baseUrl}{path}"));
    }

    private void OpenProd(string path)
    {
        var baseUrl = _prodUrl.Text.Trim().TrimEnd('/');
        Run(() => _launcher!.OpenUrl($"{baseUrl}{path}"));
    }

    private void RunScript(string name)
    {
        if (_launcher is null) return;
        SaveUrls();
        var r = _launcher.RunPowerShellScript(name);
        Log(r.Message);
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(message));
            return;
        }

        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void RunBuild()
    {
        if (_launcher is null)
        {
            Log("Cannot run — project folder not found.");
            return;
        }

        SaveUrls();
        if (_launcher.ArePilotAppsBuilt())
            Log("Pilot apps already built — you can skip Build and go to step 4 (Open Sage setup).");
        var r = _launcher.BuildPilotApps();
        Log(r.Message);
    }
}
