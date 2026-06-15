using System.IO;
using System.Runtime.InteropServices;

namespace dot_net_fm;

/// <summary>
/// Enumerates children of any Windows Shell namespace folder identified by a
/// CLSID path (e.g. "::{645FF040-...}" for Recycle Bin).
/// Uses IShellFolder COM + SHGetNameFromIDList so display names, filesystem
/// paths, and shell parsing names resolve correctly for every namespace type.
/// </summary>
internal static class ShellNamespaceEnumerator
{
    // ── Shell flags ───────────────────────────────────────────────────────────

    private const uint SFGAO_FOLDER      = 0x20000000;
    private const uint SHCONTF_FOLDERS       = 0x0020;
    private const uint SHCONTF_NONFOLDERS    = 0x0040;
    private const uint SHCONTF_INCLUDEHIDDEN = 0x0080;

    private enum SIGDN : uint
    {
        NormalDisplay          = 0x00000000,
        DesktopAbsoluteParsing = 0x80028000,
        FileSystemPath         = 0x80058000,
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHGetDesktopFolder(out IntPtr ppshf);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetNameFromIDList(
        IntPtr pidl, SIGDN sigdnName,
        [MarshalAs(UnmanagedType.LPWStr)] out string? ppszName);

    [DllImport("shell32.dll")]
    private static extern IntPtr ILCombine(IntPtr pidlParent, IntPtr pidlChild);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    // ── COM interfaces ────────────────────────────────────────────────────────

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten, out IntPtr ppidl, out uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwnd, ref Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl,
            ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwnd, uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] apidl,
            ref Guid riid, IntPtr prgfInOut,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport, Guid("000214F2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumIDList
    {
        [PreserveSig] int Next(uint celt, out IntPtr rgelt, out uint pceltFetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        void Clone(out IntPtr ppenum);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enumerates all items inside the given shell namespace folder.
    /// Each returned item has a Name (display), FullPath (filesystem or parsing),
    /// and IsFolder flag.
    /// </summary>
    public static List<FolderItem> EnumerateFolder(string shellPath)
    {
        var items = new List<FolderItem>();
        IntPtr desktopPtr = IntPtr.Zero;
        IntPtr folderPidl = IntPtr.Zero;

        try
        {
            if (SHGetDesktopFolder(out desktopPtr) != 0 || desktopPtr == IntPtr.Zero)
                return items;

            var desktop = (IShellFolder)Marshal.GetObjectForIUnknown(desktopPtr);
            try
            {
                if (SHParseDisplayName(shellPath, IntPtr.Zero, out folderPidl, 0, out _) != 0
                    || folderPidl == IntPtr.Zero)
                    return items;

                var iidShellFolder = typeof(IShellFolder).GUID;
                desktop.BindToObject(folderPidl, IntPtr.Zero, ref iidShellFolder, out IntPtr targetPtr);
                if (targetPtr == IntPtr.Zero) return items;

                var target = (IShellFolder)Marshal.GetObjectForIUnknown(targetPtr);
                try
                {
                    uint flags = SHCONTF_FOLDERS | SHCONTF_NONFOLDERS | SHCONTF_INCLUDEHIDDEN;
                    target.EnumObjects(IntPtr.Zero, flags, out IntPtr enumPtr);
                    if (enumPtr == IntPtr.Zero) return items;

                    var enumerator = (IEnumIDList)Marshal.GetObjectForIUnknown(enumPtr);
                    try
                    {
                        while (enumerator.Next(1, out IntPtr childPidl, out uint fetched) == 0 && fetched > 0)
                        {
                            try
                            {
                                var item = BuildItem(folderPidl, childPidl, target);
                                if (item != null) items.Add(item);
                            }
                            catch { }
                            finally { ILFree(childPidl); }
                        }
                    }
                    finally { Marshal.ReleaseComObject(enumerator); }
                }
                finally { Marshal.ReleaseComObject(target); }
            }
            finally { Marshal.ReleaseComObject(desktop); }
        }
        catch { }
        finally
        {
            if (folderPidl != IntPtr.Zero) ILFree(folderPidl);
            if (desktopPtr != IntPtr.Zero) Marshal.Release(desktopPtr);
        }

        return items;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static FolderItem? BuildItem(IntPtr parentPidl, IntPtr childPidl, IShellFolder parentFolder)
    {
        IntPtr absPidl = ILCombine(parentPidl, childPidl);
        if (absPidl == IntPtr.Zero) return null;

        try
        {
            SHGetNameFromIDList(absPidl, SIGDN.NormalDisplay, out string? displayName);
            if (string.IsNullOrEmpty(displayName)) return null;

            // Real filesystem path — null for purely virtual items (Network, Control Panel).
            string? fsPath = null;
            try { SHGetNameFromIDList(absPidl, SIGDN.FileSystemPath, out fsPath); } catch { }

            // Shell parsing name — navigable path even for virtual items.
            string? parsingName = null;
            try { SHGetNameFromIDList(absPidl, SIGDN.DesktopAbsoluteParsing, out parsingName); } catch { }

            string itemPath = fsPath ?? parsingName ?? displayName;

            // Check SFGAO_FOLDER on the child's relative PIDL.
            uint attrs = SFGAO_FOLDER;
            parentFolder.GetAttributesOf(1, [childPidl], ref attrs);
            bool isFolder = (attrs & SFGAO_FOLDER) != 0;

            return new FolderItem
            {
                Name     = displayName,
                FullPath = itemPath,
                IsFolder = isFolder,
                ItemCount = ResolveItemCount(fsPath, isFolder),
            };
        }
        finally { ILFree(absPidl); }
    }

    private static string ResolveItemCount(string? fsPath, bool isFolder)
    {
        if (fsPath == null) return isFolder ? "Folder" : "File";
        try
        {
            if (isFolder)
            {
                int n = Directory.EnumerateFileSystemEntries(fsPath).Count();
                return $"{n} item{(n == 1 ? "" : "s")}";
            }
            var fi = new FileInfo(fsPath);
            if (!fi.Exists) return "File";
            return fi.Length < 1024 ? $"{fi.Length} B" : $"{fi.Length / 1024} KB";
        }
        catch { return isFolder ? "Folder" : "File"; }
    }
}