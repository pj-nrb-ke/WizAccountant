using Pastel.Evolution;
using WizAccountant.Contracts;

namespace WizConnector.Setup;

public sealed class SetupForm : Form
{
    private const int FieldWidth = 360;
    private const string CommonDbDefaultName = "EvolutionCommon";

    private readonly TextBox _server = CreateField("(local)");
    private readonly TextBox _sqlUser = CreateField();
    private readonly TextBox _sqlPassword = CreateField(usePasswordChar: true);
    private readonly CheckBox _windowsAuth = new()
    {
        Text = "Use Windows login (leave SQL user/password blank)",
        AutoSize = true,
        Margin = new Padding(0, 4, 0, 8)
    };
    private readonly ComboBox _companyDb = CreateCombo();
    private readonly ComboBox _commonDb = CreateCombo();
    private readonly TextBox _licenseSerial = CreateField();
    private readonly TextBox _licenseKey = CreateField(usePasswordChar: true);
    private readonly TextBox _agentUser = CreateField("Admin");
    private readonly TextBox _agentPassword = CreateField(usePasswordChar: true);
    private readonly Label _status = new()
    {
        AutoSize = true,
        MaximumSize = new Size(540, 0),
        ForeColor = Color.DarkBlue,
        Padding = new Padding(0, 8, 0, 4)
    };

    public SetupForm()
    {
        Text = "WizConnector — Sage Setup";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 520);
        ClientSize = new Size(620, 720);

        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12)
        };

        var form = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Width = 548
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldWidth + 8));

        var row = 0;
        row = AddFieldRow(form, row, "Server host name", _server, "e.g. (local) or MY-PC\\SQLEXPRESS");
        row = AddFieldRow(form, row, "SQL Server user", _sqlUser);
        row = AddFieldRow(form, row, "SQL Server password", _sqlPassword);

        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.Controls.Add(_windowsAuth, 0, row);
        form.SetColumnSpan(_windowsAuth, 2);
        row++;

        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var btnCheck = new Button { Text = "Check", Width = 100, Height = 28, Margin = new Padding(0, 4, 0, 10) };
        btnCheck.Click += async (_, _) => await CheckSqlCredentialsAsync();
        form.Controls.Add(btnCheck, 1, row);
        row++;

        row = AddFieldRow(form, row, "Company database", _companyDb, "Choose your Sage company database");
        row = AddFieldRow(form, row, "Common database", _commonDb, "Usually EvolutionCommon");

        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var separator = new Label
        {
            Text = "Sage licence and login",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 16, 0, 4)
        };
        form.Controls.Add(separator, 0, row);
        form.SetColumnSpan(separator, 2);
        row++;

        row = AddFieldRow(form, row, "Licence serial", _licenseSerial, "From Sage SDK licence");
        row = AddFieldRow(form, row, "Licence key", _licenseKey);
        row = AddFieldRow(form, row, "Sage user", _agentUser, "Evolution login name");
        row = AddFieldRow(form, row, "Sage password", _agentPassword);

        scroll.Controls.Add(form);

        var btnTestSage = new Button { Text = "Test Sage connection", Width = 160, Height = 28 };
        var btnSave = new Button { Text = "Save (encrypted)", Width = 140, Height = 28 };
        btnTestSage.Click += async (_, _) => await TestSageConnectionAsync();
        btnSave.Click += (_, _) => SaveSettings();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 12),
            WrapContents = false
        };
        buttons.Controls.Add(btnTestSage);
        buttons.Controls.Add(btnSave);

        var info = new Label
        {
            Text = "Encrypted config: " + SageConfigStorage.EncryptedFilePath,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            MaximumSize = new Size(580, 0),
            Padding = new Padding(12, 4, 12, 0),
            ForeColor = Color.Gray
        };

        var statusPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 0, 12, 0)
        };
        statusPanel.Controls.Add(_status);

        Controls.Add(scroll);
        Controls.Add(statusPanel);
        Controls.Add(info);
        Controls.Add(buttons);

        _windowsAuth.CheckedChanged += (_, _) => ToggleSqlFields();
        ToggleSqlFields();
        LoadExisting();
    }

    private static TextBox CreateField(string? defaultText = null, bool usePasswordChar = false)
    {
        return new TextBox
        {
            Text = defaultText ?? string.Empty,
            UseSystemPasswordChar = usePasswordChar,
            Height = 23,
            Width = FieldWidth
        };
    }

    private static ComboBox CreateCombo()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = false,
            Height = 23,
            Width = FieldWidth
        };
    }

    /// <summary>Adds label + control on one row; optional hint on the next row. Returns next free row index.</summary>
    private static int AddFieldRow(TableLayoutPanel panel, int row, string label, Control control, string? hint = null)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            Margin = new Padding(0, 10, 8, 0)
        }, 0, row);

        control.Margin = new Padding(0, 6, 0, 0);
        control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        control.Width = FieldWidth;
        panel.Controls.Add(control, 1, row);
        row++;

        if (!string.IsNullOrEmpty(hint))
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label
            {
                Text = hint,
                ForeColor = Color.Gray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
                MaximumSize = new Size(FieldWidth, 0)
            }, 1, row);
            row++;
        }

        return row;
    }

    private void ToggleSqlFields()
    {
        var disabled = _windowsAuth.Checked;
        _sqlUser.Enabled = !disabled;
        _sqlPassword.Enabled = !disabled;
    }

    private void LoadExisting()
    {
        var existing = SageConfigStorage.LoadEncrypted();
        if (existing is null) return;

        _server.Text = existing.Server;
        _windowsAuth.Checked = existing.UseWindowsAuthentication;
        _sqlUser.Text = existing.SqlUser;
        _sqlPassword.Text = existing.SqlPassword;
        _licenseSerial.Text = existing.LicenseSerial;
        _licenseKey.Text = existing.LicenseKey;
        _agentUser.Text = existing.AgentUser;
        _agentPassword.Text = existing.AgentPassword;

        _companyDb.Items.Add(existing.CompanyDatabase);
        _companyDb.SelectedIndex = 0;
        _companyDb.Enabled = true;

        _commonDb.Items.Add(existing.CommonDatabase);
        _commonDb.SelectedIndex = 0;
        _commonDb.Enabled = true;

        _status.Text = "Loaded existing saved settings. Click Check to refresh database list.";
    }

    private SageConnectorConfig ReadForm()
    {
        return new SageConnectorConfig
        {
            Server = _server.Text.Trim(),
            CompanyDatabase = _companyDb.SelectedItem?.ToString() ?? _companyDb.Text.Trim(),
            CommonDatabase = _commonDb.SelectedItem?.ToString() ?? _commonDb.Text.Trim(),
            UseWindowsAuthentication = _windowsAuth.Checked,
            SqlUser = _sqlUser.Text.Trim(),
            SqlPassword = _sqlPassword.Text,
            LicenseSerial = _licenseSerial.Text.Trim(),
            LicenseKey = _licenseKey.Text.Trim(),
            AgentUser = _agentUser.Text.Trim(),
            AgentPassword = _agentPassword.Text
        };
    }

    private string? ValidateSqlCredentials()
    {
        if (string.IsNullOrWhiteSpace(_server.Text))
            return "Please enter server host name.";
        if (!_windowsAuth.Checked && string.IsNullOrWhiteSpace(_sqlUser.Text))
            return "Please enter SQL Server user name, or tick Windows login.";
        return null;
    }

    private string? ValidateForm(SageConnectorConfig config, bool requireSageFields)
    {
        var sqlError = ValidateSqlCredentials();
        if (sqlError is not null) return sqlError;

        if (string.IsNullOrWhiteSpace(config.CompanyDatabase))
            return "Please click Check and select a company database.";
        if (string.IsNullOrWhiteSpace(config.CommonDatabase))
            return "Please click Check and select a common database.";

        if (!requireSageFields) return null;

        if (string.IsNullOrWhiteSpace(config.LicenseSerial) || string.IsNullOrWhiteSpace(config.LicenseKey))
            return "Please enter licence serial and key.";
        if (string.IsNullOrWhiteSpace(config.AgentUser))
            return "Please enter Sage user name.";
        return null;
    }

    private async Task CheckSqlCredentialsAsync()
    {
        _status.Text = "Checking SQL Server…";
        _status.ForeColor = Color.DarkBlue;
        Refresh();

        var validation = ValidateSqlCredentials();
        if (validation is not null)
        {
            _status.Text = validation;
            _status.ForeColor = Color.DarkRed;
            return;
        }

        var previousCompany = _companyDb.SelectedItem?.ToString();
        var previousCommon = _commonDb.SelectedItem?.ToString();

        try
        {
            var connectionString = SqlDatabaseLister.BuildMasterConnectionString(
                _server.Text.Trim(),
                _windowsAuth.Checked,
                _sqlUser.Text.Trim(),
                _sqlPassword.Text);

            var databases = await SqlDatabaseLister.ListDatabasesAsync(connectionString);

            _companyDb.Items.Clear();
            _commonDb.Items.Clear();
            foreach (var name in databases)
            {
                _companyDb.Items.Add(name);
                _commonDb.Items.Add(name);
            }

            _companyDb.Enabled = databases.Count > 0;
            _commonDb.Enabled = databases.Count > 0;

            if (databases.Count == 0)
            {
                _status.Text = "Connected, but no user databases were found.";
                _status.ForeColor = Color.DarkRed;
                return;
            }

            SelectComboValue(_commonDb, previousCommon ?? CommonDbDefaultName);
            if (_commonDb.SelectedIndex < 0 && _commonDb.Items.Count > 0)
                _commonDb.SelectedIndex = 0;

            SelectComboValue(_companyDb, previousCompany);
            if (_companyDb.SelectedIndex < 0)
            {
                var commonName = _commonDb.SelectedItem?.ToString();
                for (var i = 0; i < _companyDb.Items.Count; i++)
                {
                    var item = _companyDb.Items[i]?.ToString();
                    if (!string.Equals(item, commonName, StringComparison.OrdinalIgnoreCase))
                    {
                        _companyDb.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (_companyDb.SelectedIndex < 0 && _companyDb.Items.Count > 0)
                _companyDb.SelectedIndex = 0;

            _status.Text = $"SQL OK — {databases.Count} database(s) loaded. Select company and common, then Test Sage or Save.";
            _status.ForeColor = Color.DarkGreen;
        }
        catch (Exception ex)
        {
            _companyDb.Enabled = false;
            _commonDb.Enabled = false;
            _status.Text = "SQL check failed: " + ex.Message;
            _status.ForeColor = Color.DarkRed;
        }
    }

    private static void SelectComboValue(ComboBox combo, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private async Task TestSageConnectionAsync()
    {
        _status.Text = "Testing Sage…";
        _status.ForeColor = Color.DarkBlue;
        Refresh();

        var config = ReadForm();
        var validation = ValidateForm(config, requireSageFields: true);
        if (validation is not null)
        {
            _status.Text = validation;
            _status.ForeColor = Color.DarkRed;
            return;
        }

        try
        {
            var count = await Task.Run(() =>
            {
                // Same sequence as Sage SDK samples / RegisterSAGESDK():
                DatabaseContext.CreateCommonDBConnection(config.BuildCommonConnectionString());
                DatabaseContext.SetLicense(config.LicenseSerial, config.LicenseKey);
                DatabaseContext.CreateConnection(config.BuildCompanyConnectionString());

                if (!string.IsNullOrWhiteSpace(config.AgentUser))
                {
                    if (!string.IsNullOrWhiteSpace(config.AgentPassword))
                    {
                        if (!Agent.Authenticate(config.AgentUser, config.AgentPassword))
                            throw new InvalidOperationException("Sage user login failed. Check Sage user and password.");
                    }

                    DatabaseContext.CurrentAgent = new Agent(config.AgentUser);
                }

                return Customer.List("DCLink > 0")?.Rows.Count ?? 0;
            });

            _status.Text = $"Sage OK — {count} customer(s) found.";
            _status.ForeColor = Color.DarkGreen;
        }
        catch (Exception ex)
        {
            _status.Text = "Sage test failed: " + ex.Message;
            _status.ForeColor = Color.DarkRed;
        }
    }

    private void SaveSettings()
    {
        var config = ReadForm();
        var validation = ValidateForm(config, requireSageFields: true);
        if (validation is not null)
        {
            MessageBox.Show(validation, "WizConnector Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            SageConfigStorage.SaveEncrypted(config);
            SageConfigStorage.SaveUserSecrets(config);

            MessageBox.Show(
                "Settings saved securely.\r\n\r\n" +
                $"Encrypted file:\r\n{SageConfigStorage.EncryptedFilePath}\r\n\r\n" +
                $"Developer secrets:\r\n{SageConfigStorage.UserSecretsFilePath}\r\n\r\n" +
                "You can now run WizConnector.Service.",
                "WizConnector Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            _status.Text = "Saved.";
            _status.ForeColor = Color.DarkGreen;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Save failed: " + ex.Message + "\r\n\r\nTry: right-click WizConnector.Setup.exe → Run as administrator",
                "WizConnector Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
