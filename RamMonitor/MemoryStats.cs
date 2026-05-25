using System.Runtime.InteropServices;

namespace RamMonitor;

internal static class MemoryStats
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public readonly record struct Snapshot(ulong CommittedBytes, ulong CommitLimitBytes);

    // ullTotalPageFile is the commit limit (physical RAM + pagefile, capped by per-process limits).
    // ullAvailPageFile is what's left, so committed = limit - available. This matches Task Manager's
    // "Committed: X / Y" line on the Performance > Memory page.
    public static Snapshot Read()
    {
        var m = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(m))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        ulong limit = m.ullTotalPageFile;
        ulong committed = limit - m.ullAvailPageFile;
        return new Snapshot(committed, limit);
    }
}
