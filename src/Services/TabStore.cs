using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace dot_net_fm;

/// <summary>
/// Owns all mutable backing data for a single tab and exposes only an
/// immutable <see cref="TabStateRecord"/> snapshot plus a read-only
/// <see cref="ReadOnlyObservableCollection{FolderItem}"/> for the grid.
/// All mutations go through <see cref="Dispatch"/>. External callers
/// never reach into internal services.
/// </summary>
public sealed class TabStore : IDisposable
{
    private TabStateRecord _state;
    private readonly NavigationService _navigation;
    private readonly IDirectoryWatcher _directoryWatcher;
    private readonly ObservableCollection<FolderItem> _folders;
    private CancellationTokenSource? _navCts;

    private const int BatchSize = 20;

    // ── Public read-only surface ───────────────────────────────────

    /// <summary>Immutable state snapshot. Changes only via reducer.</summary>
    public TabStateRecord State => _state;

    /// <summary>Read-only view of the folder items for binding.</summary>
    public ReadOnlyObservableCollection<FolderItem> Folders { get; }

    /// <summary>Single event — any UI change flows through here.</summary>
    public event Action<TabStateRecord>? StateChanged;

    // ── Construction ───────────────────────────────────────────────

    public TabStore(string userProfilePath, IFileProvider fileProvider, IIconProvider? iconProvider, IDirectoryWatcher directoryWatcher)
    {
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

        _directoryWatcher.DirectoryChanged += () => Dispatch(new TabAction.DirectoryFileChangeDetected());
    }

    // ── Dispatch — single entry point for all mutations ────────────

    public void Dispatch(TabAction action)
    {
        switch (action)
        {
            case TabAction.NavigateTo a:
                _navigation.NavigateTo(a.Path, a.PushToHistory);
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

            default:
                break;
        }

        _state = TabReducer.Reduce(_state, action);
        StateChanged?.Invoke(_state);
    }

    // ── Internal: async directory loading ──────────────────────────

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
            items = await _navigation.LoadDirectoryItemsAsync(path);
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
        if (a.Path != NavigationService.MyComputerPath)
            _directoryWatcher.Watch(a.Path);

        StateChanged?.Invoke(_state);

        _ = LoadIconsBatchedAsync(a.Items);
    }

    // ── Internal: icon loading in batches ──────────────────────────

    private async System.Threading.Tasks.Task LoadIconsBatchedAsync(IReadOnlyList<FolderItem> items)
    {
        _navCts = new CancellationTokenSource();
        var token = _navCts.Token;
        var iconSize = _state.IconSize;

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

    private async System.Threading.Tasks.Task ReloadIconsAsync(int iconSize)
    {
        _navCts = new CancellationTokenSource();
        var token = _navCts.Token;

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

    // ── Disposal ──────────────────────────────────────────────────

    public void StopWatching()
    {
        _directoryWatcher.Stop();
    }

    public void Dispose()
    {
        _navCts?.Cancel();
        _navCts?.Dispose();
        _navCts = null;

        _directoryWatcher.DirectoryChanged -= () => Dispatch(new TabAction.DirectoryFileChangeDetected());
        _directoryWatcher.Dispose();

        foreach (var item in _folders)
            item.Dispose();
        _folders.Clear();
    }
}