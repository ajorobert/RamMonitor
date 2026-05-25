using System.Windows.Forms;

namespace RamMonitor;

internal sealed class SettingsForm : Form
{
    private readonly Settings _settings;
    private readonly Action _onSaved;

    private readonly NumericUpDown _refresh = new();
    private readonly NumericUpDown _commYellow = new();
    private readonly NumericUpDown _commRed = new();
    private readonly NumericUpDown _limYellow = new();
    private readonly NumericUpDown _limRed = new();
    private readonly CheckBox _autoStart = new();
    private readonly NumericUpDown _baseline = new();
    private readonly Button _recalibrate = new();
    private readonly Button _save = new();
    private readonly Button _cancel = new();

    public SettingsForm(Settings settings, Action onSaved)
    {
        _settings = settings;
        _onSaved = onSaved;

        Text = "RamMonitor Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new System.Drawing.Size(360, 320);
        ShowInTaskbar = false;

        int y = 12;
        AddRow("Refresh interval (seconds)", _refresh, 15, 300, settings.RefreshSeconds, 0, ref y);
        AddRow("Committed yellow at (% of current limit)", _commYellow, 1, 99,
            (decimal)(settings.CommittedYellow * 100), 0, ref y);
        AddRow("Committed red at (% of current limit)", _commRed, 1, 100,
            (decimal)(settings.CommittedRed * 100), 0, ref y);
        AddRow("Limit yellow at (x baseline)", _limYellow, 1.00m, 5.00m,
            (decimal)settings.LimitYellow, 2, ref y);
        AddRow("Limit red at (x baseline)", _limRed, 1.00m, 10.00m,
            (decimal)settings.LimitRed, 2, ref y);

        var baselineLabel = new Label { Text = "Baseline limit (GB)", AutoSize = false };
        baselineLabel.SetBounds(12, y + 4, 160, 20);
        Controls.Add(baselineLabel);

        _baseline.DecimalPlaces = 2;
        _baseline.Minimum = 0.10m;
        _baseline.Maximum = 1024m;
        _baseline.Increment = 0.10m;
        _baseline.Value = Math.Clamp((decimal)BytesToGB(settings.BaselineCommitLimitBytes),
            _baseline.Minimum, _baseline.Maximum);
        _baseline.SetBounds(172, y, 80, 22);
        Controls.Add(_baseline);

        _recalibrate.SetBounds(258, y - 2, 90, 26);
        _recalibrate.Text = "Recalibrate";
        _recalibrate.Click += (_, _) =>
        {
            var snap = MemoryStats.Read();
            decimal gb = (decimal)BytesToGB(snap.CommitLimitBytes);
            _baseline.Value = Math.Clamp(gb, _baseline.Minimum, _baseline.Maximum);
        };
        Controls.Add(_recalibrate);
        y += 34;

        _autoStart.SetBounds(12, y, 320, 22);
        _autoStart.Text = "Start with Windows";
        _autoStart.Checked = AutoStart.IsEnabled();
        Controls.Add(_autoStart);
        y += 34;

        _save.SetBounds(180, y, 80, 28);
        _save.Text = "Save";
        _save.Click += (_, _) => OnSave();
        Controls.Add(_save);

        _cancel.SetBounds(268, y, 80, 28);
        _cancel.Text = "Cancel";
        _cancel.Click += (_, _) => Close();
        Controls.Add(_cancel);

        AcceptButton = _save;
        CancelButton = _cancel;
    }

    private void AddRow(string label, NumericUpDown nud, decimal min, decimal max, decimal value,
        int decimals, ref int y)
    {
        var lbl = new Label { Text = label, AutoSize = false };
        lbl.SetBounds(12, y + 4, 240, 20);
        Controls.Add(lbl);

        nud.DecimalPlaces = decimals;
        nud.Minimum = min;
        nud.Maximum = max;
        if (decimals > 0) nud.Increment = 0.01m;
        nud.Value = Math.Clamp(value, min, max);
        nud.SetBounds(258, y, 90, 22);
        Controls.Add(nud);
        y += 28;
    }

    private void OnSave()
    {
        _settings.RefreshSeconds = (int)_refresh.Value;
        _settings.CommittedYellow = (double)_commYellow.Value / 100.0;
        _settings.CommittedRed = (double)_commRed.Value / 100.0;
        _settings.LimitYellow = (double)_limYellow.Value;
        _settings.LimitRed = (double)_limRed.Value;
        _settings.BaselineCommitLimitBytes = (ulong)((double)_baseline.Value * 1024 * 1024 * 1024);

        try
        {
            bool was = AutoStart.IsEnabled();
            if (_autoStart.Checked && !was) AutoStart.Enable();
            else if (!_autoStart.Checked && was) AutoStart.Disable();
            _settings.StartWithWindows = _autoStart.Checked;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not update auto-start: {ex.Message}",
                "RamMonitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _settings.Save();
        _onSaved();
        Close();
    }

    private static double BytesToGB(ulong bytes) => bytes / 1024.0 / 1024.0 / 1024.0;
}
