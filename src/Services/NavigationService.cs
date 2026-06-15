using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace dot_net_fm;

/// <summary>
/// Handles all navigation state and logic: NavigateTo, LoadDirectory, back/forward/up stacks.
/// Delegates file listing to <see cref="IFileProvider"/> — no direct filesystem calls.
/// Icon loading is orchestrated per-item by FolderItem.LoadIconAsync(), not by this service.
/// </summary>
public class NavigationService
{
    private readonly string _userProfilePath;
    private readonly IFileProvider _fileProvider;
    private readonly IIconProvider? _iconProvider;
    private string _currentPath = "";
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    public string CurrentPath => _currentPath;

    public event Action<string>? DirectoryLoaded;
    public event Action<string>? TitleChanged;
    public event Action<string>? StatusChanged;
    public event Action? NavStateChanged;

    public NavigationService(string userProfilePath, IFileProvider fileProvider, IIconProvider? iconProvider = null)
    {
        _userProfilePath = userProfilePath;
        _fileProvider = fileProvider;
        _iconProvider = iconProvider;
    }

    /// <summary>
    /// Navigates to a directory, optionally pushing the current path onto the back stack.
    /// </summary>
    public void NavigateTo(string targetPath, bool pushToHistory = true)
    {
        if (string.IsNullOrEmpty(targetPath)) return;

        bool isVirtualRoot = _fileProvider.IsVirtualRoot(targetPath);
        if (!isVirtualRoot && !Directory.Exists(targetPath)) return;

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
    /// Loads directory contents via <see cref="IFileProvider"/>. Items are returned with
    /// the icon provider set. The caller populates the UI and triggers per-item icon loads.
    /// </summary>
    public async Task<List<FolderItem>> LoadDirectoryItemsAsync(string targetPath)
    {
        TitleChanged?.Invoke(_fileProvider.GetDisplayTitle(targetPath));

        var items = new List<FolderItem>();
        try
        {
            var result = await _fileProvider.GetItemsAsync(targetPath, 0, int.MaxValue);

            foreach (var item in result.Items)
            {
                item.IconProvider = _iconProvider;

                // Sync icon for immediate display (drives, folders)
                if (_iconProvider != null && item.NativeIcon == null)
                    item.NativeIcon = _iconProvider.GetIconForFile(item.FullPath);

                items.Add(item);
            }

            string? freeSpaceStr = _fileProvider.GetFreeSpaceInfo(targetPath);
            string status = $"{items.Count} item{(items.Count == 1 ? "" : "s")}";
            if (!string.IsNullOrEmpty(freeSpaceStr))
                status += $", {freeSpaceStr}";

            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not access folder: {ex.Message}", "Access Denied",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

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
        var parent = _fileProvider.GetParentPath(_currentPath);
        if (parent == null) return false;
        NavigateTo(parent);
        NavStateChanged?.Invoke();
        return true;
    }

    public bool CanGoBack    => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;
    public bool CanGoUp      => !_fileProvider.IsVirtualRoot(_currentPath) && _fileProvider.GetParentPath(_currentPath) != null;

    public void RefreshNavState() => NavStateChanged?.Invoke();
}