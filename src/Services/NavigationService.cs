using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace dot_net_fm;

/// <summary>
/// Handles all navigation state and logic: NavigateTo, LoadDirectory, back/forward/up stacks.
/// No UI dependencies — only data and state.
/// Icon loading is orchestrated per-item by FolderItem.LoadIconAsync(), not by this service.
/// </summary>
public class NavigationService
{
    public const string MyComputerPath = "::mycomputer";

    private readonly string _userProfilePath;
    private string _currentPath = "";
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    public string CurrentPath => _currentPath;

    public event Action<string>? DirectoryLoaded;
    public event Action<string>? TitleChanged;
    public event Action<string>? StatusChanged;
    public event Action? NavStateChanged;

    public NavigationService(string userProfilePath)
    {
        _userProfilePath = userProfilePath;
    }

    /// <summary>
    /// Navigates to a directory, optionally pushing the current path onto the back stack.
    /// </summary>
    public void NavigateTo(string targetPath, bool pushToHistory = true)
    {
        if (string.IsNullOrEmpty(targetPath)) return;

        bool isSpecialPath = targetPath == MyComputerPath;
        if (!isSpecialPath && !Directory.Exists(targetPath)) return;

        if (pushToHistory && !string.IsNullOrEmpty(_currentPath))
        {
            _backStack.Push(_currentPath);
            _forwardStack.Clear();
        }

        _currentPath = targetPath;
        NavStateChanged?.Invoke();
        DirectoryLoaded?.Invoke(targetPath);
    }

    /// <summary>
    /// Loads directory contents. Items are returned without icons.
    /// The caller (MainWindow) populates the UI and triggers per-item icon loads.
    /// </summary>
    public async Task<List<FolderItem>> LoadDirectoryItemsAsync(string targetPath)
    {
        if (targetPath == MyComputerPath)
        {
            TitleChanged?.Invoke("My Computer");
            var drives = DriveInfoService.GetDriveItems();
            StatusChanged?.Invoke($"{drives.Count} drive{(drives.Count == 1 ? "" : "s")}");
            return drives;
        }

        string displayName = targetPath.Equals(_userProfilePath, StringComparison.OrdinalIgnoreCase)
            ? Environment.UserName
            : Path.GetFileName(targetPath);

        if (string.IsNullOrEmpty(displayName)) displayName = targetPath;

        TitleChanged?.Invoke(displayName);

        var items = new List<FolderItem>();
        try
        {
            var entries = await Task.Run(() => ReadDirectoryEntries(targetPath));
            items.AddRange(entries);

            int count = items.Count;
            string freeSpaceStr = "";
            string? driveName = Path.GetPathRoot(targetPath);
            if (!string.IsNullOrEmpty(driveName))
            {
                try
                {
                    var drive = new DriveInfo(driveName);
                    double gb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    freeSpaceStr = $", Free space: {gb:F1} GB";
                }
                catch { }
            }

            StatusChanged?.Invoke($"{count} item{(count == 1 ? "" : "s")}{freeSpaceStr}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not access folder: {ex.Message}", "Access Denied",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return items;
    }

    private static List<FolderItem> ReadDirectoryEntries(string targetPath)
    {
        var items = new List<FolderItem>();

        foreach (var dir in Directory.GetDirectories(targetPath))
        {
            try
            {
                var di = new DirectoryInfo(dir);
                if ((di.Attributes & FileAttributes.Hidden) != 0 ||
                    (di.Attributes & FileAttributes.System) != 0) continue;

                string itemCount;
                try
                {
                    int n = Directory.GetFileSystemEntries(dir).Length;
                    itemCount = $"{n} item{(n == 1 ? "" : "s")}";
                }
                catch { itemCount = "Folder"; }

                items.Add(new FolderItem { Name = di.Name, ItemCount = itemCount, FullPath = dir, IsFolder = true });
            }
            catch { }
        }

        foreach (var file in Directory.GetFiles(targetPath))
        {
            try
            {
                var fi = new FileInfo(file);
                if ((fi.Attributes & FileAttributes.Hidden) != 0 ||
                    (fi.Attributes & FileAttributes.System) != 0) continue;

                items.Add(new FolderItem
                {
                    Name = fi.Name,
                    ItemCount = $"{fi.Length / 1024} KB",
                    FullPath = file,
                    IsFolder = false
                });
            }
            catch { }
        }

        // Default sort: folders first, then by extension, then by name (case-insensitive).
        items.Sort((a, b) =>
        {
            int folderCmp = -a.IsFolder.CompareTo(b.IsFolder); // true (folder) before false (file)
            if (folderCmp != 0) return folderCmp;

            string extA = Path.GetExtension(a.Name);
            string extB = Path.GetExtension(b.Name);
            int extCmp = string.Compare(extA, extB, StringComparison.OrdinalIgnoreCase);
            if (extCmp != 0) return extCmp;

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return items;
    }

    public bool GoBack()
    {
        if (_backStack.Count == 0) return false;
        _forwardStack.Push(_currentPath);
        _currentPath = _backStack.Pop();
        NavStateChanged?.Invoke();
        DirectoryLoaded?.Invoke(_currentPath);
        return true;
    }

    public bool GoForward()
    {
        if (_forwardStack.Count == 0) return false;
        _backStack.Push(_currentPath);
        _currentPath = _forwardStack.Pop();
        NavStateChanged?.Invoke();
        DirectoryLoaded?.Invoke(_currentPath);
        return true;
    }

    public bool GoUp()
    {
        var parent = Directory.GetParent(_currentPath);
        if (parent == null) return false;
        NavigateTo(parent.FullName);
        NavStateChanged?.Invoke();
        return true;
    }

    public bool CanGoBack    => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;
    public bool CanGoUp      => Directory.GetParent(_currentPath) != null;

    public void RefreshNavState() => NavStateChanged?.Invoke();
}
