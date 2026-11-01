using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DotNetFM;

internal static class FolderPicker
{
    private const uint FosPickFolders = 0x20;
    private const uint FosForceFileSystem = 0x40;
    private const uint FosPathMustExist = 0x800;
    private const uint SigdnFileSysPath = 0x80058000;

    public static string? Show(Window? owner, string title, string? initialFolder)
    {
        IFileOpenDialog? dialog = null;
        try
        {
            dialog = (IFileOpenDialog)new FileOpenDialogComObject();

            dialog.GetOptions(out uint options);
            dialog.SetOptions(options | FosPickFolders | FosForceFileSystem | FosPathMustExist);
            dialog.SetTitle(title);

            if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
                SetInitialFolder(dialog, initialFolder!);

            IntPtr ownerHandle = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            int hr = dialog.Show(ownerHandle);
            if (hr != 0) return null;

            dialog.GetResult(out IShellItem? item);
            if (item == null) return null;

            item.GetDisplayName(SigdnFileSysPath, out IntPtr pathPtr);
            string? result = Marshal.PtrToStringUni(pathPtr);
            Marshal.FreeCoTaskMem(pathPtr);
            Marshal.ReleaseComObject(item);
            return result;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (dialog != null) Marshal.ReleaseComObject(dialog);
        }
    }

    private static void SetInitialFolder(IFileOpenDialog dialog, string folder)
    {
        try
        {
            Guid shellItemGuid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
            SHCreateItemFromParsingName(folder, IntPtr.Zero, ref shellItemGuid, out IShellItem? item);
            if (item != null) dialog.SetFolder(item);
        }
        catch
        {
        }
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogComObject
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr hwndOwner);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void GetResults(out IntPtr ppenum);
        void GetSelectedItems(out IntPtr ppsai);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem? ppv);
}
