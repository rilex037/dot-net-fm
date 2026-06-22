using System.Runtime.InteropServices;

namespace DotNetFM;

/// <summary>
/// Win32 P/Invokes and structs for per-monitor DPI-aware window
/// management. All members are scoped to the minimum visibility
/// required — the only public surface is
/// <see cref="ConstrainWindowToWorkArea"/>.
/// </summary>
internal static partial class NativeMethods
{
    internal const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 2u;

    // ── P/Invokes ───────────────────────────────────────────────────

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // ── Structs (private; exposed only within class) ─────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ── Public surface ─────────────────────────────────────────────

    /// <summary>
    /// Handle <c>WM_GETMINMAXINFO</c> by constraining the maximized
    /// window size to the monitor's work area. Call this from your
    /// <c>HwndSource</c> hook when <c>msg == 0x0024</c>.
    /// </summary>
    internal static void ConstrainWindowToWorkArea(IntPtr hwnd, IntPtr lParam, ref bool handled)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            var rc = monitorInfo.rcWork;
            mmi.ptMaxPosition.x = rc.Left;
            mmi.ptMaxPosition.y = rc.Top;
            mmi.ptMaxSize.x = rc.Right - rc.Left;
            mmi.ptMaxSize.y = rc.Bottom - rc.Top;
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
    }
}
