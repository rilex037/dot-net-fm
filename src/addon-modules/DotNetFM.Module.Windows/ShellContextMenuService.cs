using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DotNetFM;

public static class ShellContextMenuService
{
    private const uint CMD_FIRST = 1;

    private const uint TPM_LEFTALIGN    = 0x0000;
    private const uint TPM_RETURNCMD    = 0x0100;
    private const uint TPM_RIGHTBUTTON  = 0x0002;
    private const uint TPM_NONOTIFY     = 0x0080;

    private const uint CMIC_MASK_UNICODE       = 0x4000;
    private const uint CMIC_MASK_PTINVOKE      = 0x20000000;
    private const uint CMF_NORMAL              = 0x00000000;
    private const uint CMF_EXTENDEDVERBS       = 0x00000100;

    private const uint SW_SHOWNORMAL = 1;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        IntPtr hWnd,
        IntPtr tpmParams);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(
            IntPtr hwnd,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten,
            out IntPtr ppidl,
            out uint pdwAttributes);

        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        void CreateViewObject(IntPtr hwnd, ref Guid riid, out IntPtr ppv);

        void GetAttributesOf(
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl,
            ref uint rgfInOut);

        void GetUIObjectOf(
            IntPtr hwnd,
            uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl,
            ref Guid riid,
            IntPtr prgfInOut,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        void GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);

        void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
    }

    [ComImport]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        void QueryContextMenu(
            IntPtr hMenu,
            uint indexMenu,
            uint idCmdFirst,
            uint idCmdLast,
            uint uFlags);

        void InvokeCommand(IntPtr pici);

        void GetCommandString(
            IntPtr idCmd,
            uint uType,
            IntPtr pReserved,
            IntPtr pszName,
            uint cchMax);
    }

    [ComImport]
    [Guid("000214F4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2 : IContextMenu
    {
        new void QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        new void InvokeCommand(IntPtr pici);
        new void GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport]
    [Guid("000214F5-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3 : IContextMenu2
    {
        new void QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        new void InvokeCommand(IntPtr pici);
        new void GetCommandString(IntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
        new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        [PreserveSig]
        int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, ref IntPtr pResult);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int  cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpDirectory;
        public int  nShow;
        public int  dwHotKey;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.LPStr)] public string? lpTitle;
        public IntPtr lpVerbW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParametersW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectoryW;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpTitleW;
        public POINT ptInvoke;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TPMPARAMS
    {
        public int cbSize;
        public RECT rcExclude;
    }

    public static void Show(Window ownerWindow, Point screenPoint, IReadOnlyList<string> selectedPaths)
    {
        if (selectedPaths == null || selectedPaths.Count == 0) return;

        string? parentDir = GetCommonParentDirectory(selectedPaths);
        if (parentDir == null) return;

        IntPtr hwnd = ResolveHwnd(ownerWindow);

        int hr = SHGetDesktopFolder(out var desktopFolder);
        if (hr != 0) return;

        try
        {
            hr = SHParseDisplayName(parentDir, IntPtr.Zero, out IntPtr parentPidl, 0, out _);
            if (hr != 0 || parentPidl == IntPtr.Zero) return;

            try
            {
                Guid iidShellFolder = new("000214E6-0000-0000-C000-000000000046");
                desktopFolder.BindToObject(parentPidl, IntPtr.Zero, ref iidShellFolder, out IntPtr parentFolderPtr);
                if (parentFolderPtr == IntPtr.Zero) return;

                var parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(parentFolderPtr);
                try
                {
                    var itemPidls = new IntPtr[selectedPaths.Count];
                    int validCount = 0;

                    for (int i = 0; i < selectedPaths.Count; i++)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(selectedPaths[i]);
                            if (string.IsNullOrEmpty(fileName)) continue;

                            uint pchEaten;
                            uint pdwAttrib;
                            parentFolder.ParseDisplayName(hwnd, IntPtr.Zero, fileName, out pchEaten, out itemPidls[validCount], out pdwAttrib);
                            validCount++;
                        }
                        catch
                        {
                        }
                    }

                    if (validCount == 0) return;

                    try
                    {
                        if (validCount < selectedPaths.Count)
                        {
                            Array.Resize(ref itemPidls, validCount);
                        }

                        Guid iidContextMenu = new("000214E4-0000-0000-C000-000000000046");
                        parentFolder.GetUIObjectOf(IntPtr.Zero, (uint)validCount, itemPidls, ref iidContextMenu, IntPtr.Zero, out object contextMenuObj);

                        if (contextMenuObj is IContextMenu contextMenu)
                        {
                            try
                            {
                                ShowMenu(hwnd, screenPoint, contextMenu);
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(contextMenu);
                            }
                        }
                    }
                    finally
                    {
                        for (int i = 0; i < itemPidls.Length; i++)
                        {
                            if (itemPidls[i] != IntPtr.Zero)
                                ILFree(itemPidls[i]);
                        }
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(parentFolder);
                    Marshal.Release(parentFolderPtr);
                }
            }
            finally
            {
                ILFree(parentPidl);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(desktopFolder);
        }
    }

    public static bool ShowBackground(Window ownerWindow, Point screenPoint, string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath)) return false;

        IntPtr hwnd = ResolveHwnd(ownerWindow);

        int hr = SHGetDesktopFolder(out var desktopFolder);
        if (hr != 0) return false;

        try
        {
            hr = SHParseDisplayName(directoryPath, IntPtr.Zero, out IntPtr dirPidl, 0, out _);
            if (hr != 0 || dirPidl == IntPtr.Zero) return false;

            try
            {
                Guid iidShellFolder = new("000214E6-0000-0000-C000-000000000046");
                desktopFolder.BindToObject(dirPidl, IntPtr.Zero, ref iidShellFolder, out IntPtr folderPtr);
                if (folderPtr == IntPtr.Zero) return false;

                var folder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
                try
                {
                    Guid iidContextMenu = new("000214E4-0000-0000-C000-000000000046");
                    folder.CreateViewObject(hwnd, ref iidContextMenu, out IntPtr contextMenuPtr);
                    if (contextMenuPtr == IntPtr.Zero) return false;

                    var contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPtr);
                    try
                    {
                        ShowMenu(hwnd, screenPoint, contextMenu);
                        return true;
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(contextMenu);
                        Marshal.Release(contextMenuPtr);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(folder);
                    Marshal.Release(folderPtr);
                }
            }
            finally
            {
                ILFree(dirPidl);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(desktopFolder);
        }
    }

    private static IntPtr ResolveHwnd(Window ownerWindow)
    {
        IntPtr hwnd = IntPtr.Zero;
        if (ownerWindow != null)
        {
            var hwndSource = PresentationSource.FromVisual(ownerWindow) as HwndSource;
            hwnd = hwndSource?.Handle ?? IntPtr.Zero;
        }

        if (hwnd == IntPtr.Zero)
            hwnd = GetForegroundWindow();

        return hwnd;
    }

    private static void ShowMenu(IntPtr hwnd, Point screenPoint, IContextMenu contextMenu)
    {
        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        var messageHelper = new ContextMenuMessageHelper(hwnd, contextMenu);

        try
        {
            contextMenu.QueryContextMenu(hMenu, 0, CMD_FIRST, 0x7FFF, CMF_NORMAL | CMF_EXTENDEDVERBS);

            SetForegroundWindow(hwnd);

            var tpmParams = new TPMPARAMS
            {
                cbSize = Marshal.SizeOf<TPMPARAMS>(),
                rcExclude = new RECT()
            };

            IntPtr pTpmParams = Marshal.AllocHGlobal(Marshal.SizeOf(tpmParams));
            try
            {
                Marshal.StructureToPtr(tpmParams, pTpmParams, false);

                uint cmd = TrackPopupMenuEx(
                    hMenu,
                    TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
                    (int)screenPoint.X,
                    (int)screenPoint.Y,
                    hwnd,
                    pTpmParams);

                if (cmd >= CMD_FIRST)
                {
                    uint verbIndex = cmd - CMD_FIRST;

                    var invokeInfo = new CMINVOKECOMMANDINFOEX
                    {
                        cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                        fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE,
                        hwnd = hwnd,
                        lpVerb = (IntPtr)verbIndex,
                        lpParameters = null,
                        lpDirectory = null,
                        nShow = (int)SW_SHOWNORMAL,
                        dwHotKey = 0,
                        hIcon = IntPtr.Zero,
                        lpTitle = null,
                        lpVerbW = (IntPtr)verbIndex,
                        lpParametersW = null,
                        lpDirectoryW = null,
                        lpTitleW = null,
                        ptInvoke = new POINT((int)screenPoint.X, (int)screenPoint.Y)
                    };

                    IntPtr pInvokeInfo = Marshal.AllocHGlobal(invokeInfo.cbSize);
                    try
                    {
                        Marshal.StructureToPtr(invokeInfo, pInvokeInfo, false);
                        contextMenu.InvokeCommand(pInvokeInfo);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pInvokeInfo);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pTpmParams);
            }
        }
        catch
        {
        }
        finally
        {
            messageHelper.Dispose();
            DestroyMenu(hMenu);
            PostMessage(hwnd, 0x001E, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private sealed class ContextMenuMessageHelper : IDisposable
    {
        private readonly IntPtr _hwnd;
        private readonly IContextMenu _contextMenu;
        private HwndSourceHook? _hook;

        public ContextMenuMessageHelper(IntPtr hwnd, IContextMenu contextMenu)
        {
            _hwnd = hwnd;
            _contextMenu = contextMenu;

            var source = HwndSource.FromHwnd(hwnd);
            if (source != null)
            {
                _hook = WndProc;
                source.AddHook(_hook);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INITMENUPOPUP = 0x0111;
            const int WM_DRAWITEM = 0x002B;
            const int WM_MEASUREITEM = 0x002C;

            switch ((uint)msg)
            {
                case WM_INITMENUPOPUP:
                case WM_DRAWITEM:
                case WM_MEASUREITEM:
                    if (_contextMenu is IContextMenu3 cm3)
                    {
                        IntPtr result = IntPtr.Zero;
                        cm3.HandleMenuMsg2((uint)msg, wParam, lParam, ref result);
                        if ((uint)msg == WM_DRAWITEM || (uint)msg == WM_MEASUREITEM)
                            handled = true;
                    }
                    else if (_contextMenu is IContextMenu2 cm2)
                    {
                        cm2.HandleMenuMsg((uint)msg, wParam, lParam);
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_hook != null)
            {
                var source = HwndSource.FromHwnd(_hwnd);
                source?.RemoveHook(_hook);
                _hook = null;
            }
        }
    }

    private static string? GetCommonParentDirectory(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return null;

        string? parent = Path.GetDirectoryName(paths[0]);
        if (parent == null) return null;

        for (int i = 1; i < paths.Count; i++)
        {
            string? otherParent = Path.GetDirectoryName(paths[i]);
            if (!string.Equals(parent, otherParent, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return parent;
    }
}
