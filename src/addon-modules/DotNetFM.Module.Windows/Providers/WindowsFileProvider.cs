using System.IO;
using System.Runtime.InteropServices;

namespace DotNetFM;

internal static class ShellPathHelper
{
    public static bool IsShellPath(string path) =>
        path.StartsWith("::{", StringComparison.OrdinalIgnoreCase)
        || path.Equals(MyComputerPath, StringComparison.OrdinalIgnoreCase);

    public const string MyComputerPath = "::mycomputer";

    public static string? GetShellParent(string path)
    {
        if (path.Equals(MyComputerPath, StringComparison.OrdinalIgnoreCase)) return null;
        return MyComputerPath;
    }
}

public sealed class WindowsFileProvider : IFileProvider
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    public const string MyComputerPath = ShellPathHelper.MyComputerPath;

    public async Task<FileResult> GetItemsAsync(string path, int offset, int count, string sortKey, bool sortAscending, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sortKey)) sortKey = "name";

        if (ShellPathHelper.IsShellPath(path))
        {
            var items = path == MyComputerPath
                ? GetDriveItems()
                : await Task.Run(() => ShellNamespaceEnumerator.EnumerateFolder(path), cancellationToken);

            return new FileResult
            {
                Items = items,
                TotalCount = items.Count,
                Offset = 0
            };
        }

        return await Task.Run(() =>
        {
            var items = new List<FolderItem>();

            try
            {
                bool showHidden = AppStore.ReadBool("view.showhidden", false);

                var dirInfo = new DirectoryInfo(ResolveDirectoryTarget(new DirectoryInfo(path)));

                foreach (var fsi in dirInfo.EnumerateFileSystemInfos())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!showHidden && (fsi.Attributes & FileAttributes.Hidden) != 0)
                        continue;

                    try
                    {
                        if (fsi is DirectoryInfo di)
                        {
                            string targetPath = ResolveDirectoryTarget(di);
                            string itemCount = CountEntries(targetPath, showHidden);

                            items.Add(new FolderItem
                            {
                                Name = di.Name,
                                ItemCount = itemCount,
                                FullPath = di.FullName,
                                IsFolder = true,
                                SortDate = di.LastWriteTime
                            });
                        }
                        else if (fsi is FileInfo fi)
                        {
                            items.Add(new FolderItem
                            {
                                Name = fi.Name,
                                ItemCount = FileSizeFormatter.Format(fi.Length),
                                FullPath = fi.FullName,
                                IsFolder = false,
                                SortDate = fi.LastWriteTime,
                                SortSize = fi.Length,
                                SortType = Path.GetExtension(fi.Name).ToLowerInvariant()
                            });
                        }
                    }
                    catch { }
                }

                items.Sort((a, b) => CompareItems(a, b, sortKey, sortAscending));

                static int CompareItems(FolderItem a, FolderItem b, string sortKey, bool ascending)
                {
                    int folderCmp = -a.IsFolder.CompareTo(b.IsFolder);
                    if (folderCmp != 0) return folderCmp;

                    int cmp = sortKey switch
                    {
                        "size" => a.SortSize != b.SortSize
                            ? a.SortSize.CompareTo(b.SortSize)
                            : StrCmpLogicalW(a.Name, b.Name),
                        "type" => a.SortType.Equals(b.SortType, StringComparison.OrdinalIgnoreCase)
                            ? StrCmpLogicalW(a.Name, b.Name)
                            : string.CompareOrdinal(a.SortType, b.SortType),
                        "date" => a.SortDate != b.SortDate
                            ? a.SortDate.CompareTo(b.SortDate)
                            : StrCmpLogicalW(a.Name, b.Name),
                        _ => StrCmpLogicalW(a.Name, b.Name)
                    };

                    return ascending ? cmp : -cmp;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return new FileResult
            {
                Items = items,
                TotalCount = items.Count,
                Offset = 0
            };
        }, cancellationToken);
    }

    public string GetDisplayTitle(string path)
    {
        if (WindowsModule.KnownClsids.TryGetValue(path, out var displayName)) return displayName;
        if (string.IsNullOrEmpty(path)) return path;
        string name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    public string GetDisplayPath(string path)
    {
        if (WindowsModule.KnownClsids.TryGetValue(path, out var displayName)) return displayName;
        return path;
    }

    public string? GetParentPath(string path)
    {
        if (ShellPathHelper.IsShellPath(path))
            return ShellPathHelper.GetShellParent(path);
        var parent = Directory.GetParent(path);
        return parent?.FullName;
    }

    public bool IsVirtualRoot(string path) => ShellPathHelper.IsShellPath(path);

    public bool PathExists(string path) => ShellPathHelper.IsShellPath(path) || Directory.Exists(path);

    public string? GetFreeSpaceInfo(string path)
    {
        if (ShellPathHelper.IsShellPath(path)) return null;
        try
        {
            var driveName = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(driveName)) return null;
            var drive = new DriveInfo(driveName);
            double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            return $"Free space: {freeGB:F1} GB";
        }
        catch { return null; }
    }

    internal static string ResolveDirectoryTarget(DirectoryInfo di)
    {
        if (!di.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return di.FullName;

        string? target = di.LinkTarget;
        if (string.IsNullOrEmpty(target))
            return di.FullName;

        if (target.StartsWith(@"\\?\"))
            target = target[4..];

        return target;
    }

    private static string CountEntries(string path, bool showHidden)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            int n = showHidden
                ? dir.EnumerateFileSystemInfos().Count()
                : dir.EnumerateFileSystemInfos().Count(fsi => (fsi.Attributes & FileAttributes.Hidden) == 0);

            return n == 0 ? "Empty Folder" : $"{n} item{(n == 1 ? "" : "s")}";
        }
        catch
        {
            return "???";
        }
    }

    private static List<FolderItem> GetDriveItems()
    {
        var items = new List<FolderItem>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network))
                continue;
            if (!drive.IsReady) continue;

            string root = drive.RootDirectory.FullName.TrimEnd('\\');
            string displayName = string.IsNullOrEmpty(drive.VolumeLabel)
                ? root
                : $"{drive.VolumeLabel} ({root})";

            double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            double totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);

            items.Add(new FolderItem
            {
                Name = displayName,
                ItemCount = $"{freeGB:F1} GB free of {totalGB:F0} GB",
                FullPath = drive.Name,
                IsFolder = true
            });
        }
        return items;
    }
}
