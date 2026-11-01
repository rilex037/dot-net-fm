using System.Windows;

namespace DotNetFM;

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

    public bool NavigateTo(string targetPath, bool pushToHistory = true)
    {
        if (string.IsNullOrEmpty(targetPath)) return false;
        if (!_fileProvider.PathExists(targetPath)) return false;

        if (pushToHistory && !string.IsNullOrEmpty(_currentPath))
        {
            _backStack.Push(_currentPath);
            _forwardStack.Clear();
        }

        _currentPath = targetPath;
        NavStateChanged?.Invoke();
        DirectoryLoaded?.Invoke(targetPath);
        return true;
    }

    public async Task<List<FolderItem>> LoadDirectoryItemsAsync(string targetPath, string sortKey, bool sortAscending)
    {
        TitleChanged?.Invoke(_fileProvider.GetDisplayTitle(targetPath));

        var items = new List<FolderItem>();
        try
        {
            var result = await _fileProvider.GetItemsAsync(targetPath, 0, int.MaxValue, sortKey, sortAscending);

            foreach (var item in result.Items)
            {
                item.IconProvider = _iconProvider;

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
        catch (OperationCanceledException) { }
        catch (UnauthorizedAccessException)
        {
            StatusChanged?.Invoke("Access denied");
            CustomDialog.Show("Access Denied",
                $"Could not access \u201c{targetPath}\u201d.\nYou may not have permission to open this location.",
                DialogIcon.Error);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Could not load this location");
            CustomDialog.Show("Loading Error",
                $"Could not load \u201c{targetPath}\u201d:\n{ex.Message}",
                DialogIcon.Error);
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
        return NavigateTo(parent);
    }

    public bool CanGoBack    => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;
    public bool CanGoUp      => !_fileProvider.IsVirtualRoot(_currentPath) && _fileProvider.GetParentPath(_currentPath) != null;
}
