using System.Collections.ObjectModel;

namespace DotNetFM;

public sealed class TabManager
{
    private readonly string _userProfilePath;
    private readonly IModule? _module;
    private readonly IFileProvider _fileProvider;
    private readonly IIconProvider? _iconProvider;
    private readonly List<TabStore> _stores = new();

    public ReadOnlyCollection<TabStore> Tabs { get; }

    public TabStore? ActiveTab { get; private set; }

    public event Action<TabStore?>? ActiveTabChanged;

    public event Action<Guid>? TabClosed;

    public event Action? AllTabsClosed;

    public TabManager(string userProfilePath, IModule? module)
    {
        _userProfilePath = userProfilePath;
        _module = module;
        _fileProvider = module?.FileProvider ?? new DummyFileProvider();
        _iconProvider = module?.IconProvider;
        Tabs = new ReadOnlyCollection<TabStore>(_stores);
    }

    public TabStore AddTab(string? initialPath = null, string? sortKey = null, bool? sortAscending = null)
    {
        var watcher = _module?.CreateDirectoryWatcher() ?? new DummyDirectoryWatcher();
        var store = new TabStore(_userProfilePath, _fileProvider, _iconProvider, watcher);
        _stores.Add(store);
        SetActiveTab(store);

        if (!string.IsNullOrEmpty(sortKey))
            store.Dispatch(new TabAction.SetSort(sortKey, sortAscending ?? true));

        string targetPath = initialPath ?? "";
        if (string.IsNullOrEmpty(targetPath) || !_fileProvider.PathExists(targetPath))
            targetPath = _userProfilePath;

        store.Dispatch(new TabAction.NavigateTo(targetPath, PushToHistory: false));

        return store;
    }

    public bool CloseTab(Guid tabId)
    {
        int index = FindIndex(tabId);
        if (index < 0) return false;

        var store = _stores[index];
        store.StopWatching();
        _stores.RemoveAt(index);
        store.Dispose();

        bool wasActive = ActiveTab != null && ActiveTab.State.TabId == tabId;

        if (wasActive)
        {
            ActiveTab = null;

            if (_stores.Count > 0)
            {
                int next = Math.Min(index, _stores.Count - 1);
                SetActiveTab(_stores[next]);
            }
            else
            {
                AllTabsClosed?.Invoke();
            }
        }

        TabClosed?.Invoke(tabId);

        return true;
    }

    public void SetActiveTab(Guid tabId)
    {
        var store = FindStore(tabId);
        if (store != null) SetActiveTab(store);
    }

    public void SetActiveTab(int index)
    {
        if (index >= 0 && index < _stores.Count)
            SetActiveTab(_stores[index]);
    }

    private void SetActiveTab(TabStore store)
    {
        if (ActiveTab?.State.TabId == store.State.TabId) return;
        ActiveTab = store;
        ActiveTabChanged?.Invoke(store);
    }

    public void Dispatch(Guid tabId, TabAction action)
    {
        FindStore(tabId)?.Dispatch(action);
    }

    public void DispatchActive(TabAction action)
    {
        ActiveTab?.Dispatch(action);
    }

    public void CommitActiveRename(IFileView view)
    {
        view.CommitAnyRename();
    }

    private TabStore? FindStore(Guid tabId)
    {
        foreach (var s in _stores)
            if (s.State.TabId == tabId) return s;
        return null;
    }

    private int FindIndex(Guid tabId)
    {
        for (int i = 0; i < _stores.Count; i++)
            if (_stores[i].State.TabId == tabId) return i;
        return -1;
    }
}

file sealed class DummyFileProvider : IFileProvider
{
    public Task<FileResult> GetItemsAsync(string path, int offset, int count, string sortKey, bool sortAscending, CancellationToken cancellationToken = default)
        => Task.FromResult(new FileResult { Items = [], TotalCount = 0, Offset = 0 });
    public string GetDisplayTitle(string path) => path;
    public string GetDisplayPath(string path) => path;
    public string? GetParentPath(string path) => null;
    public bool IsVirtualRoot(string path) => true;
    public bool PathExists(string path) => true;
    public string? GetFreeSpaceInfo(string path) => null;
}

#pragma warning disable CS0067
file sealed class DummyDirectoryWatcher : IDirectoryWatcher
{
    public event Action? DirectoryChanged;
#pragma warning restore CS0067
    public void Watch(string path) { }
    public void Stop() { }
    public void Dispose() { }
}
