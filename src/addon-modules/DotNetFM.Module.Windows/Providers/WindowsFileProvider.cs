using System.IO;

namespace DotNetFM;

/// <summary>
/// Helpers for detecting and resolving Windows Shell CLSID virtual paths
/// (e.g. "::{645FF040-5081-101B-9F08-00AA002F954E}" for Recycle Bin).
/// </summary>
internal static class ShellPathHelper
{
    /// <summary>True if the path is a CLSID or ::mycomputer virtual shell path.</summary>
    public static bool IsShellPath(string path) =>
        path.StartsWith("::{", StringComparison.OrdinalIgnoreCase)
        || path.Equals(MyComputerPath, StringComparison.OrdinalIgnoreCase);

    public const string MyComputerPath = "::mycomputer";

    /// <summary>
    /// Maps known CLSIDs to their parent. Currently everything goes to
    /// MyComputer (Recycle Bin, This PC, Network, Control Panel).
    /// </summary>
    public static string? GetShellParent(string path)
    {
        if (path.Equals(MyComputerPath, StringComparison.OrdinalIgnoreCase)) return null;
        return MyComputerPath;
    }
}

/// <summary>
/// Windows-specific IFileProvider implementation for local filesystem browsing.
/// Handles drive detection, directory listing with the "My Computer" virtual root,
/// and shell namespace folders (Recycle Bin, This PC, Network, Control Panel).
/// </summary>
public sealed class WindowsFileProvider : IFileProvider
{
    /// <summary>Special path representing "My Computer" view.</summary>
    public const string MyComputerPath = ShellPathHelper.MyComputerPath;

    public async Task<FileResult> GetItemsAsync(string path, int offset, int count, CancellationToken cancellationToken = default)
    {
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
                // Resolve junctions/symlinks so enumeration hits the real target,
                // while the caller keeps the original path for display.
                var dirInfo = new DirectoryInfo(ResolveDirectoryTarget(new DirectoryInfo(path)));

                // Single pass over the directory contents (files and folders together)
                foreach (var fsi in dirInfo.EnumerateFileSystemInfos())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        if (fsi is DirectoryInfo di)
                        {
                            string targetPath = ResolveDirectoryTarget(di);

                            string itemCount;
                            try
                            {
                                int n = Directory.EnumerateFileSystemEntries(targetPath).Count();
                                itemCount = n == 0
                                    ? "Empty Folder"
                                    : $"{n} item{(n == 1 ? "" : "s")}";
                            }
                            catch 
                            { 
                                itemCount = "???"; 
                            }

                            items.Add(new FolderItem 
                            { 
                                Name = di.Name, 
                                ItemCount = itemCount, 
                                FullPath = di.FullName, 
                                IsFolder = true 
                            });
                        }
                        else if (fsi is FileInfo fi)
                        {
                            items.Add(new FolderItem
                            {
                                Name = fi.Name,
                                ItemCount = fi.Length < 1024 
                                    ? $"{fi.Length} B" 
                                    : $"{fi.Length / 1024} KB",
                                FullPath = fi.FullName,
                                IsFolder = false
                            });
                        }
                    }
                    catch { }
                }

                // Sort: folders first, then by extension (files), then by name
                items.Sort((a, b) =>
                {
                    int folderCmp = -a.IsFolder.CompareTo(b.IsFolder);
                    if (folderCmp != 0) return folderCmp;

                    if (!a.IsFolder)
                    {
                        string extA = Path.GetExtension(a.Name);
                        string extB = Path.GetExtension(b.Name);
                        int extCmp = string.Compare(extA, extB, StringComparison.OrdinalIgnoreCase);
                        if (extCmp != 0) return extCmp;
                    }

                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
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

    /// <summary>
    /// If <paramref name="di"/> is a junction or symlink, returns the resolved
    /// target path. Otherwise returns <see cref="DirectoryInfo.FullName"/> unchanged.
    /// </summary>
    private static string ResolveDirectoryTarget(DirectoryInfo di)
    {
        if (!di.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return di.FullName;

        string? target = di.LinkTarget;
        if (string.IsNullOrEmpty(target))
            return di.FullName;

        // NTFS junctions store the target as \??\C:\path — strip the prefix.
        if (target.StartsWith(@"\\?\"))
            target = target[4..];

        return target;
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