namespace WizConnector.Tray;

internal sealed class PairingForm : Form
{
    private readonly TrayAppContext _app;
    private readonly TextBox _code = new() { Width = 200, CharacterCasing = CharacterCasing.Upper };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(360, 0) };

    public PairingForm(TrayAppContext app)
    {
        _app = app;
        Text = "Pair WizConnector";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 160);

        var layout = new TableLayoutPanel { Padding = new Padding(12), ColumnCount = 2, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Pairing code", AutoSize = true }, 0, 0);
        layout.Controls.Add(_code, 1, 0);
        layout.SetColumnSpan(_status, 2);
        layout.Controls.Add(_status, 0, 1);

        var pair = new Button { Text = "Pair", Width = 90 };
        pair.Click += async (_, _) => await PairAsync();
        var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(12) };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(pair);
        Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = pair;
        CancelButton = cancel;
    }

    private async Task PairAsync()
    {
        _status.ForeColor = Color.DarkBlue;
        _status.Text = "Pairing…";
        try
        {
            var ok = await _app.PairWithCodeAsync(_code.Text.Trim());
            if (!ok)
            {
                _status.ForeColor = Color.DarkRed;
                _status.Text = "Pairing failed. Check code and API URL in Status window.";
                return;
            }

            _status.ForeColor = Color.DarkGreen;
            _status.Text = "Paired. Start WizConnector.Service, then refresh status.";
            await Task.Delay(1200);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _status.ForeColor = Color.DarkRed;
            _status.Text = ex.Message;
        }
    }
}
