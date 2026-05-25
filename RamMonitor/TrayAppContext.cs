using System.Drawing;
using System.Windows.Forms;

namespace RamMonitor;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly System.Threading.Timer _timer;
    private readonly SynchronizationContext _uiSync;
    private Settings _settings;
    private SettingsForm? _settingsForm;

    // Cache of last rendered frame so we skip redraws when nothing changed.
    private string _lastTop = "";
    private IconRenderer.Band _lastTopBand = (IconRenderer.Band)(-1);
    private IconRenderer.Band _lastBottomBand = (IconRenderer.Band)(-1);

    public TrayAppContext()
    {
        _uiSync = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settings = Settings.Load();

        // First-run: capture baseline from current commit limit (system is presumed healthy).
        if (_settings.BaselineCommitLimitBytes == 0)
        {
            _settings.BaselineCommitLimitBytes = MemoryStats.Read().CommitLimitBytes;
            _settings.Save();
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = menu,
            Text = "RamMonitor",
            Icon = SystemIcons.Application, // placeholder; replaced on first tick
        };

        Tick(null); // initial render

        _timer = new System.Threading.Timer(Tick, null,
            TimeSpan.FromSeconds(_settings.RefreshSeconds),
            TimeSpan.FromSeconds(_settings.RefreshSeconds));
    }

    private void Tick(object? state)
    {
        try
        {
            var snap = MemoryStats.Read();
            double commGB = snap.CommittedBytes / 1024.0 / 1024.0 / 1024.0;
            double limGB = snap.CommitLimitBytes / 1024.0 / 1024.0 / 1024.0;
            double committedRatio = snap.CommitLimitBytes == 0
                ? 0
                : (double)snap.CommittedBytes / snap.CommitLimitBytes;
            double limitRatio = _settings.BaselineCommitLimitBytes == 0
                ? 1.0
                : (double)snap.CommitLimitBytes / _settings.BaselineCommitLimitBytes;

            var textBand = BandFromCommitted(committedRatio);
            var bgBand = BandFromLimit(limitRatio);

            string display = FormatGB(commGB);

            if (display == _lastTop
                && textBand == _lastTopBand && bgBand == _lastBottomBand)
                return;

            _lastTop = display;
            _lastTopBand = textBand;
            _lastBottomBand = bgBand;

            var newIcon = IconRenderer.Render(display, textBand, bgBand);
            string tooltip = $"Committed {commGB:F1} GB / Limit {limGB:F1} GB";
            if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 63);

            _uiSync.Post(_ =>
            {
                var old = _tray.Icon;
                _tray.Icon = newIcon;
                _tray.Text = tooltip;
                old?.Dispose();
            }, null);
        }
        catch
        {
            // Swallow transient errors; next tick will retry. Tray-app silent-fail is preferred
            // over popping dialogs from a background timer.
        }
    }

    private IconRenderer.Band BandFromCommitted(double ratio)
    {
        if (ratio >= _settings.CommittedRed) return IconRenderer.Band.Red;
        if (ratio >= _settings.CommittedYellow) return IconRenderer.Band.Yellow;
        return IconRenderer.Band.Green;
    }

    private IconRenderer.Band BandFromLimit(double ratio)
    {
        if (ratio >= _settings.LimitRed) return IconRenderer.Band.Red;
        if (ratio >= _settings.LimitYellow) return IconRenderer.Band.Yellow;
        return IconRenderer.Band.Green;
    }

    private static string FormatGB(double gb)
    {
        // Fit two characters where possible: e.g. "24" for 24 GB, "9.4" for under 10.
        if (gb >= 10) return ((int)Math.Round(gb)).ToString();
        return gb.ToString("0.0");
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_settings, OnSettingsSaved);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
    }

    private void OnSettingsSaved()
    {
        _timer.Change(TimeSpan.FromSeconds(_settings.RefreshSeconds),
            TimeSpan.FromSeconds(_settings.RefreshSeconds));
        // Force a redraw on next tick by invalidating the cache.
        _lastTopBand = (IconRenderer.Band)(-1);
        Tick(null);
    }

    private void ExitApp()
    {
        _timer.Dispose();
        _tray.Visible = false;
        var old = _tray.Icon;
        _tray.Icon = null;
        old?.Dispose();
        _tray.Dispose();
        ExitThread();
    }
}
