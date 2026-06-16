using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace DotNetFM;

/// <summary>
/// Shell icon / thumbnail helper.
/// NO caching — every call fetches fresh from the shell at the exact requested size.
/// Memory is owned by the caller (FolderItem) and released on navigation.
/// Bounded concurrency on thumbnail loads (4 shell calls max) to avoid
/// thread-pool starvation and shell saturation on large folders.
/// </summary>
public static class NativeIconHelper
{
    // ── Concurrency cap ─────────────────────────────────────────────────────
    // Shell IShellItemImageFactory calls are blocking native COM operations.
    // Without a cap, 1000+ items flood the thread pool and the shell stops responding.
    private static readonly SemaphoreSlim ThumbnailGate = new(4, 4);

    // ── Win32 — IShellItemImageFactory (hi-res icons + thumbnails) ───────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [Flags]
    private enum SIIGBF
    {
        ResizeToFit   = 0x00,
        BiggerSizeOk  = 0x01,
        IconOnly      = 0x04,
        ThumbnailOnly = 0x08,
    }

    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

    // NOT readonly — passed as 'ref' to SHCreateItemFromParsingName, which requires a writable reference
    private static Guid IID_IShellItemImageFactory = new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

    // ── Win32 — SHGetFileInfo (fallback 32×32 icon) ──────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int    iIcon;
        public uint   dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]  public string szTypeName;
    }

    private const uint SHGFI_ICON      = 0x0100;
    private const uint SHGFI_LARGEICON = 0x0000;

    // ── Public API (no caching — fresh shell call every time) ────────────────

    /// <summary>
    /// Fast synchronous 32×32 icon for a file via SHGetFileInfo.
    /// Every call fetches from the shell — no cache.
    /// </summary>
    public static BitmapSource? GetIconForFile(string filePath)
        => FetchIconViaShgfi(filePath);

    /// <summary>
    /// Async hi-res thumbnail for files at the exact requested pixel size.
    /// Falls back to hi-res shell icon. Every call fetches from the shell — no cache.
    /// Concurrency is capped to 4 simultaneous shell calls.
    /// </summary>
    public static async Task<BitmapSource?> GetThumbnailAsync(string filePath, int requestedSize, CancellationToken cancellationToken = default)
    {
        await ThumbnailGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(
                () => FetchFromImageFactory(filePath, requestedSize, SIIGBF.BiggerSizeOk | SIIGBF.ResizeToFit),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ThumbnailGate.Release();
        }
    }

    // ── Hi-res icon via IShellItemImageFactory ────────────────────────────────

    private static BitmapSource? FetchFromImageFactory(string path, int size, SIIGBF flags)
    {
        try
        {
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref IID_IShellItemImageFactory, out var obj);
            if (obj is IShellItemImageFactory factory)
            {
                try
                {
                    int hr = factory.GetImage(new SIZE { cx = size, cy = size }, flags, out var hBitmap);
                    if (hr == 0 && hBitmap != IntPtr.Zero)
                    {
                        var bs = HBitmapToBitmapSource(hBitmap);
                        if (bs != null) return bs;
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
        }
        catch { }

        // Fallback — 32×32 from SHGetFileInfo
        return FetchIconViaShgfi(path);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BitmapSource? FetchIconViaShgfi(string path)
    {
        var shfi = new SHFILEINFO();
        return SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi),
                   SHGFI_ICON | SHGFI_LARGEICON) != IntPtr.Zero
            && shfi.hIcon != IntPtr.Zero
            ? IconToBitmapSource(shfi.hIcon)
            : null;
    }

    private static BitmapSource? HBitmapToBitmapSource(IntPtr hBitmap)
    {
        try
        {
            var bs = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        catch { return null; }
        finally { DeleteObject(hBitmap); }
    }

    private static BitmapSource? IconToBitmapSource(IntPtr hIcon)
    {
        try
        {
            var bs = Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        catch { return null; }
        finally { DestroyIcon(hIcon); }
    }
}
