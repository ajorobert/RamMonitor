using System.IO;

namespace RamMonitor;

internal static class AutoStart
{
    private const string ShortcutName = "RamMonitor.lnk";

    private static string StartupFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private static string ShortcutPath => Path.Combine(StartupFolder, ShortcutName);

    private static string ExePath =>
        Environment.ProcessPath
        ?? throw new InvalidOperationException("Could not determine the running process path.");

    public static bool IsEnabled() => File.Exists(ShortcutPath);

    public static void Enable()
    {
        Directory.CreateDirectory(StartupFolder);

        // Late-bound COM: WScript.Shell -> IWshShortcut. Avoids referencing IWshRuntimeLibrary.
        Type? wshType = Type.GetTypeFromProgID("WScript.Shell");
        if (wshType is null)
            throw new InvalidOperationException("WScript.Shell COM type not available.");

        dynamic shell = Activator.CreateInstance(wshType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            try
            {
                shortcut.TargetPath = ExePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(ExePath) ?? string.Empty;
                shortcut.Description = "RAM commit-pressure tray monitor";
                shortcut.WindowStyle = 7; // minimized; harmless for a tray-only app
                shortcut.Save();
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    public static void Disable()
    {
        if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
    }
}
