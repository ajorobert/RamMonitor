using System.IO;
using System.Text.Json;

namespace RamMonitor;

internal sealed class Settings
{
    public int RefreshSeconds { get; set; } = 30;

    // Committed / CurrentLimit thresholds (0..1)
    public double CommittedYellow { get; set; } = 0.70;
    public double CommittedRed { get; set; } = 0.85;

    // CurrentLimit / Baseline thresholds (>= 1.0)
    public double LimitYellow { get; set; } = 1.02;
    public double LimitRed { get; set; } = 1.15;

    // Healthy commit limit captured at first run; user can recalibrate.
    public ulong BaselineCommitLimitBytes { get; set; }

    public bool StartWithWindows { get; set; }

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamMonitor");

    private static string ConfigPath => Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<Settings>(json);
                if (loaded is not null) return loaded;
            }
        }
        catch { /* fall through to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
    }
}
