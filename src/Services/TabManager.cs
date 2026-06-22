using System.Collections.ObjectModel;

namespace DotNetFM;

/// <summary>
/// Manages a collection of <see cref="TabStore"/> instances and routes
/// actions to the correct tab via <see cref="Guid"/> state IDs.
/// </summary>
public sealed class TabManager
{
    private readonly string _userProfilePath;
    private readonly IModule? _module;
    private readonly IFileProvider _fileProvider;
    private readonly IIconProvider? _iconProvider;
    private readonly List<TabStore> _stores = new();

    /// <summary>All open tabs in display order (for tab strip binding).</summary>
    public ReadOnlyCollection<TabStore> Tabs { get; }

    /// <summary>The currently active (focused) tab, or null if no tabs are open.</summary>
    public TabStore? ActiveTab { get; private set; }

    /// <summary>Fired when the active tab changes (tab selected, tab closed, etc.).</summary>
    public event Action<TabStore?>? ActiveTabChanged;

    /// <summary>Fired when a tab is closed.</summary>
    public event Action<Guid>? TabClosed;

    /// <summary>Fired when the last tab is closed.</summary>
    public event Action? AllTabsClosed;

    public TabManager(string userProfilePath, IModule? module)
    {
        _userProfilePath = userProfilePath;
        _module = module;
        _fileProvider = module?.FileProvider ?? new DummyFileProvider();
        _iconProvider = module?.IconProvider;
        Tabs = new ReadOnlyCollection<TabStore>(_stores);
    }

    // ── Tab lifecycle ─────────────────────────────────────────────

    /// <summary>
    /// Creates a new tab, optionally navigating to an initial path.
    /// The new tab becomes the active tab.
    /// </summary>
    public TabStore AddTab(string? initialPath = null)
    {
        var watcher = _module?.CreateDirectoryWatcher() ?? new DummyDirectoryWatcher();
        var store = new TabStore(_userProfilePath, _fileProvider, _iconProvider, watcher);
        _stores.Add(store);
        SetActiveTab(store);

        store.Dispatch(new TabAction.NavigateTo(
            initialPath ?? _userProfilePath, PushToHistory: false));

        return store;
    }

    /// <summary>
    /// Closes the tab identified by <paramref name="tabId"/>.
    /// If it was the active tab, switches to an adjacent one.
    /// Returns true if the tab was found and removed.
    /// </summary>
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

        // Fire event so MainWindow can synchronize the tab strip UI
        TabClosed?.Invoke(tabId);

        return true;
    }

    // ── Active tab selection ───────────────────────────────────────

    /// <summary>Sets the active tab by its state ID.</summary>
    public void SetActiveTab(Guid tabId)
    {
        var store = FindStore(tabId);
        if (store != null) SetActiveTab(store);
    }

    /// <summary>Sets the active tab by index.</summary>
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

    // ── Action routing ────────────────────────────────────────────

    /// <summary>
    /// Dispatches an action to the tab identified by <paramref name="tabId"/>.
    /// </summary>
    public void Dispatch(Guid tabId, TabAction action)
    {
        FindStore(tabId)?.Dispatch(action);
    }

    /// <summary>
    /// Dispatches an action to the currently active tab.
    /// </summary>
    public void DispatchActive(TabAction action)
    {
        ActiveTab?.Dispatch(action);
    }

    /// <summary>
    /// Commits pending renames on the active tab before a navigation.
    /// Delegates to <see cref="IFileView.CommitAnyRename"/>.
    /// </summary>
    public void CommitActiveRename(IFileView view)
    {
        view.CommitAnyRename();
    }

    // ── Lookup ────────────────────────────────────────────────────

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

/// <summary>
/// No-op fallback implementations used when no module is loaded.
/// Prevents null-reference issues while maintaining the interface contract.
/// </summary>
file sealed class DummyFileProvider : IFileProvider
{
    public Task<FileResult> GetItemsAsync(string path, int offset, int count, CancellationToken cancellationToken = default)
        => Task.FromResult(new FileResult { Items = [], TotalCount = 0, Offset = 0 });
    public string GetDisplayTitle(string path) => path;
    public string GetDisplayPath(string path) => path;
    public string? GetParentPath(string path) => null;
    public bool IsVirtualRoot(string path) => true;
    public bool PathExists(string path) => true;
    public string? GetFreeSpaceInfo(string path) => null;
}

#pragma warning disable CS0067 // DummyDirectoryWatcher.DirectoryChanged is never used — no-op fallback
file sealed class DummyDirectoryWatcher : IDirectoryWatcher
{
    public event Action? DirectoryChanged;
#pragma warning restore CS0067
    public void Watch(string path) { }
    public void Stop() { }
    public void Dispose() { }
}
