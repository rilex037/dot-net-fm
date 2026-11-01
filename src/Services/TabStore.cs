using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace DotNetFM;

public sealed class TabStore : IDisposable
{
    private TabStateRecord _state;
    private readonly NavigationService _navigation;
    private readonly IFileProvider _fileProvider;
    private readonly IDirectoryWatcher _directoryWatcher;
    private readonly ObservableCollection<FolderItem> _folders;
    private CancellationTokenSource? _navCts;
    private readonly Action _directoryChangedHandler;
    private readonly Dictionary<string, double> _scrollOffsets = new();

    private const int BatchSize = 20;

    public TabStateRecord State => _state;

    public ReadOnlyObservableCollection<FolderItem> Folders { get; }

    public event Action<TabStateRecord>? StateChanged;

    public TabStore(string userProfilePath, IFileProvider fileProvider, IIconProvider? iconProvider, IDirectoryWatcher directoryWatcher)
    {
        _fileProvider = fileProvider;
        _navigation = new NavigationService(userProfilePath, fileProvider, iconProvider);
        _directoryWatcher = directoryWatcher;
        _folders = new ObservableCollection<FolderItem>();
        Folders = new ReadOnlyObservableCollection<FolderItem>(_folders);
        _state = new TabStateRecord();

        _navigation.DirectoryLoaded += OnNavigationDirectoryLoaded;
        _navigation.TitleChanged   += title  => Dispatch(new TabAction.TitleUpdated(title));
        _navigation.StatusChanged  += text   => Dispatch(new TabAction.StatusTextUpdated(text));
        _navigation.NavStateChanged += ()    => Dispatch(new TabAction.NavStateUpdated(
            _navigation.CanGoBack, _navigation.CanGoForward, _navigation.CanGoUp));

        _directoryChangedHandler = () => Dispatch(new TabAction.DirectoryFileChangeDetected());
        _directoryWatcher.DirectoryChanged += _directoryChangedHandler;
    }

    public void Dispatch(TabAction action)
    {
        switch (action)
        {
            case TabAction.NavigateTo a:
                if (!_navigation.NavigateTo(a.Path, a.PushToHistory))
                {
                    _state = TabReducer.Reduce(_state, new TabAction.StatusTextUpdated("Path not found"));
                    StateChanged?.Invoke(_state);
                    return;
                }
                break;

            case TabAction.GoBack:
                _navigation.GoBack();
                break;

            case TabAction.GoForward:
                _navigation.GoForward();
                break;

            case TabAction.GoUp:
                _navigation.GoUp();
                break;

            case TabAction.DirectoryLoaded a:
                ApplyDirectoryLoaded(a);
                return;

            case TabAction.BeginRefresh:
                if (_state.LoadStatus is TabLoadStatus.Loading or TabLoadStatus.Refreshing) return;
                if (string.IsNullOrEmpty(_state.ActivePath)) return;
                _navigation.NavigateTo(_state.ActivePath, pushToHistory: false);
                break;

            case TabAction.DirectoryFileChangeDetected:
                if (_state.LoadStatus != TabLoadStatus.Idle) return;
                if (string.IsNullOrEmpty(_state.ActivePath)) return;
                if (AnyItemEditing()) return;
                _navigation.NavigateTo(_state.ActivePath, pushToHistory: false);
                break;

            case TabAction.SetIconSize a:
                _navCts?.Cancel();
                _navCts?.Dispose();
                _navCts = null;

                foreach (var item in _folders)
                    item.NativeIcon = null;

                _state = TabReducer.Reduce(_state, action);
                StateChanged?.Invoke(_state);

                if (!string.IsNullOrEmpty(_state.ActivePath))
                    _ = ReloadIconsAsync(a.Size);
                return;

            case TabAction.SetSort:
                _state = TabReducer.Reduce(_state, action);
                StateChanged?.Invoke(_state);

                if (!string.IsNullOrEmpty(_state.ActivePath) &&
                    !_fileProvider.IsVirtualRoot(_state.ActivePath))
                {
                    _state = TabReducer.Reduce(_state, new TabAction.BeginRefresh());
                    StateChanged?.Invoke(_state);
                    _navigation.NavigateTo(_state.ActivePath, pushToHistory: false);
                }
                return;

            default:
                break;
        }

        _state = TabReducer.Reduce(_state, action);
        StateChanged?.Invoke(_state);
    }

    private bool AnyItemEditing()
    {
        foreach (var item in _folders)
        {
            if (item.IsEditing) return true;
        }
        return false;
    }

    private async void OnNavigationDirectoryLoaded(string path)
    {
        _navCts?.Cancel();
        _navCts?.Dispose();
        _navCts = null;

        foreach (var old in _folders)
            old.Dispose();

        List<FolderItem> items;
        try
        {
            items = await _navigation.LoadDirectoryItemsAsync(path, _state.SortKey, _state.SortAscending);
        }
        catch
        {
            Dispatch(new TabAction.StatusTextUpdated("Error loading directory"));
            return;
        }

        Dispatch(new TabAction.DirectoryLoaded(path, items));
    }

    private void ApplyDirectoryLoaded(TabAction.DirectoryLoaded a)
    {
        _folders.Clear();
        foreach (var item in a.Items)
            _folders.Add(item);

        _state = TabReducer.Reduce(_state, a);

        _directoryWatcher.Stop();
        if (!_fileProvider.IsVirtualRoot(a.Path))
            _directoryWatcher.Watch(a.Path);

        StateChanged?.Invoke(_state);

        _ = LoadIconsBatchedAsync(a.Items);
    }

    private async System.Threading.Tasks.Task LoadIconsBatchedAsync(IReadOnlyList<FolderItem> items)
    {
        _navCts = new CancellationTokenSource();
        var cts = _navCts;
        var token = cts.Token;
        var iconSize = _state.IconSize;

        try
        {
            await Application.Current.Dispatcher.InvokeAsync(
                () => { }, DispatcherPriority.Background);

            for (int off = 0; off < items.Count; off += BatchSize)
            {
                if (token.IsCancellationRequested) break;

                int end = Math.Min(off + BatchSize, items.Count);
                for (int i = off; i < end; i++)
                    items[i].LoadIconAsync(token, iconSize);

                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }
        finally
        {
            if (ReferenceEquals(_navCts, cts))
            {
                cts.Dispose();
                _navCts = null;
            }
        }
    }

    private async System.Threading.Tasks.Task ReloadIconsAsync(int iconSize)
    {
        _navCts = new CancellationTokenSource();
        var cts = _navCts;
        var token = cts.Token;

        try
        {
            await Application.Current.Dispatcher.InvokeAsync(
                () => { }, DispatcherPriority.Background);

            for (int off = 0; off < _folders.Count; off += BatchSize)
            {
                if (token.IsCancellationRequested) break;

                int end = Math.Min(off + BatchSize, _folders.Count);
                for (int i = off; i < end; i++)
                    _folders[i].LoadIconAsync(token, iconSize);

                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }
        finally
        {
            if (ReferenceEquals(_navCts, cts))
            {
                cts.Dispose();
                _navCts = null;
            }
        }
    }

    public void SaveScrollOffset(string path, double offset)
    {
        _scrollOffsets[path] = offset;
    }

    public double GetScrollOffset(string path)
    {
        return _scrollOffsets.TryGetValue(path, out var offset) ? offset : 0;
    }

    public void StopWatching()
    {
        _directoryWatcher.Stop();
    }

    public void Dispose()
    {
        _navCts?.Cancel();
        _navCts?.Dispose();
        _navCts = null;

        _directoryWatcher.DirectoryChanged -= _directoryChangedHandler;
        _directoryWatcher.Dispose();

        foreach (var item in _folders)
            item.Dispose();
        _folders.Clear();
    }
}
