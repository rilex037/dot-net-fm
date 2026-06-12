using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace dot_net_fm;

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

    // ── Win32 — SHGetFileInfo (fallback 32�-32 icon) ──────────────────────────

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

    private const uint SHGFI_ICON           = 0x0100;
    private const uint SHGFI_LARGEICON      = 0x0000;
    private const uint SHGFI_USEFILEATTRIBS = 0x0010;
    private const uint FILE_ATTRIBUTE_NORMAL= 0x0080;
    private const uint FILE_ATTRIBUTE_DIR   = 0x0010;

    // ── Public API (no caching — fresh shell call every time) ────────────────

    /// <summary>
    /// Fast synchronous 32�-32 icon for a file via SHGetFileInfo.
    /// Every call fetches from the shell — no cache.
    /// </summary>
    public static BitmapSource? GetIconForFile(string filePath)
        => FetchIconViaShgfi(filePath, FILE_ATTRIBUTE_NORMAL, useAttribs: false);

    /// <summary>
    /// Synchronous hi-res icon for a directory via IShellItemImageFactory at the given size.
    /// Every call fetches from the shell — no cache.
    /// </summary>
    public static BitmapSource? GetIconForDirectory(string dirPath, int size = 256)
        => FetchHiResIcon(dirPath, size);

    /// <summary>
    /// Async hi-res thumbnail for files at the exact requested pixel size.
    /// Falls back to hi-res shell icon. Every call fetches from the shell — no cache.
    /// Concurrency is capped to 4 simultaneous shell calls.
    /// </summary>
    public static async Task<BitmapSource?> GetThumbnailAsync(string filePath, int requestedSize)
    {
        await ThumbnailGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => FetchThumbnailOrHiResIcon(filePath, requestedSize)).ConfigureAwait(false);
        }
        finally
        {
            ThumbnailGate.Release();
        }
    }

    // ── Hi-res icon via IShellItemImageFactory ────────────────────────────────

    private static BitmapSource? FetchHiResIcon(string path, int size = 256)
    {
        try
        {
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref IID_IShellItemImageFactory, out var obj);
            if (obj is IShellItemImageFactory factory)
            {
                int hr = factory.GetImage(new SIZE { cx = size, cy = size },
                    SIIGBF.BiggerSizeOk | SIIGBF.IconOnly, out var hBitmap);

                if (hr == 0 && hBitmap != IntPtr.Zero)
                {
                    var bs = HBitmapToBitmapSource(hBitmap);
                    if (bs != null) return bs;
                }
            }
        }
        catch { }

        // Fallback — 32�-32 from SHGetFileInfo
        return FetchIconViaShgfi(path, FILE_ATTRIBUTE_DIR, useAttribs: false);
    }

    private static BitmapSource? FetchThumbnailOrHiResIcon(string filePath, int requestedSize)
    {
        try
        {
            SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref IID_IShellItemImageFactory, out var obj);
            if (obj is IShellItemImageFactory factory)
            {
                int hr = factory.GetImage(new SIZE { cx = requestedSize, cy = requestedSize },
                    SIIGBF.BiggerSizeOk | SIIGBF.ResizeToFit, out var hBitmap);

                if (hr == 0 && hBitmap != IntPtr.Zero)
                {
                    var bs = HBitmapToBitmapSource(hBitmap);
                    if (bs != null) return bs;
                }
            }
        }
        catch { }

        return FetchIconViaShgfi(filePath, FILE_ATTRIBUTE_NORMAL, useAttribs: false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BitmapSource? FetchIconViaShgfi(string path, uint attr, bool useAttribs)
    {
        var shfi  = new SHFILEINFO();
        var flags = SHGFI_ICON | SHGFI_LARGEICON;
        if (useAttribs) flags |= SHGFI_USEFILEATTRIBS;

        return SHGetFileInfo(path, attr, ref shfi, (uint)Marshal.SizeOf(shfi), flags) != IntPtr.Zero
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
