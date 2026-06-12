using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace dot_net_fm;

/// <summary>
/// Main window: thin layout shell assembling all UserControls
/// and wiring up services. No business logic - delegates to services.
/// </summary>
public partial class MainWindow : Window
{
    public ObservableCollection<FolderItem> Folders { get; } = new();
    public ObservableCollection<SidebarItem> MyComputerItems { get; } = new();
    public ObservableCollection<SidebarItem> NetworkItems { get; } = new();
    public ObservableCollection<SidebarItem> BookmarkItems { get; } = new();

    private readonly NavigationService _navigation;
    private readonly KeybindingService _keybinding;
    private readonly FileInteractionService _interaction;
    private readonly DirectoryWatcherService _directoryWatcher = new();
    private bool _isRefreshing;

    // Cancellation scope for the current batch of icon loads.
    // Signalled on navigation - cancels all in-flight LoadIconAsync calls immediately
    // so old FolderItems become unreachable and GC reclaims their memory.
    private CancellationTokenSource? _navCts;

    // Process at most this many items per batch to keep peak working set small.
    private const int BatchSize = 20;

    public MainWindow()
    {
        InitializeComponent();

        string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _navigation = new NavigationService(userProfilePath);
        _keybinding = new KeybindingService();
        _interaction = new FileInteractionService();

        SidebarPanel.MyComputerItems = MyComputerItems;
        SidebarPanel.NetworkItems    = NetworkItems;
        SidebarPanel.BookmarkItems   = BookmarkItems;

        FileGrid.Folders            = Folders;
        FileGrid.InteractionService = _interaction;

        InitializeSidebar();
        WireUpNavigationEvents();
        WireUpKeybindingEvents();
        WireUpInteractionEvents();
        WireUpUIEvents();

        StatusBar.ZoomChanged += size =>
        {
            FileGrid.IconSize = size;

            // Zoom changed - cancel any in-flight icon loads at the old size
            // and re-load all visible items at the new pixel size.
            // This ensures we only hold BitmapSources sized for the current zoom,
            // not 256×256 for a 24×24 display.
            _navCts?.Cancel();
            _navCts?.Dispose();
            _navCts = null;

            // Dispose current NativeIcon on all items - forces re-fetch at new size
            foreach (var item in Folders)
                item.NativeIcon = null;

            // If we have a current path, re-load icons at the new zoom level
            if (!string.IsNullOrEmpty(_navigation.CurrentPath))
                _ = ReloadIconsAtCurrentZoom(size);
        };
        StatusBar.ZoomSlider.Value = 3;

        _navigation.NavigateTo(userProfilePath, pushToHistory: false);
    }

    private void InitializeSidebar()
    {
        SidebarConfig config = SidebarConfigService.Load();
        SidebarIconMapper.Initialize(config.SidebarIcons);

        string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string userName = Environment.UserName;

        foreach (var section in config.Sections)
        {
            var target = section.Name.Equals("Network", StringComparison.OrdinalIgnoreCase)
                ? NetworkItems
                : MyComputerItems;

            foreach (var entry in section.Items)
            {
                string resolvedPath = SidebarConfigService.ResolvePath(entry.Path);
                string displayName  = entry.Name;
                if (entry.Name.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                    resolvedPath.Equals(userProfilePath, StringComparison.OrdinalIgnoreCase))
                    displayName = userName;

                target.Add(new SidebarItem
                {
                    Name     = displayName,
                    IconPath = SidebarIconMapper.GetIconPath(entry.Icon),
                    Path     = resolvedPath,
                });
            }
        }

        foreach (var bm in config.Bookmarks)
        {
            BookmarkItems.Add(new SidebarItem
            {
                Name     = bm.Name,
                IconPath = SidebarIconMapper.GetIconPath(bm.Icon),
                Path     = SidebarConfigService.ResolvePath(bm.Path),
            });
        }
    }

    // --- Event wiring ---

    private void WireUpNavigationEvents()
    {
        _directoryWatcher.DirectoryChanged += OnDirectoryChanged;

        _navigation.DirectoryLoaded += async path =>
        {
            // 1. Cancel all in-flight icon loads from the previous navigation
            //    This makes the old FolderItems unreachable so GC can reclaim them.
            _navCts?.Cancel();
            _navCts?.Dispose();
            _navCts = null;

            // 2. Dispose old items to release their BitmapSources immediately
            //    (belt-and-suspenders with the cancellation above)
            foreach (var old in Folders)
                old.Dispose();

            // 3. Fetch directory entries (background thread, no icons yet)
            var items = await _navigation.LoadDirectoryItemsAsync(path);

            // 4. Populate collection - WPF renders the grid with no icons (instant)
            _interaction.CommitActiveRename(Folders, FileGrid.FolderItemsControl);
            Folders.Clear();
            foreach (var item in items)
                Folders.Add(item);

            _navigation.RefreshNavState();

            // Start watching the current directory for external changes
            if (path != NavigationService.MyComputerPath)
                _directoryWatcher.Watch(path);

            // 5. Force full GC collection - disposed BitmapSources are freed now
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);

            // 6. Yield one frame so the grid renders before we start background work
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            // 7. Load icons in small batches so peak working set stays low.
            //    Each batch yields a frame so the UI stays responsive and already-loaded
            //    thumbnails are visible immediately.
            //    Icons are fetched at exactly the current zoom level - no oversized
            //    BitmapSources wasting memory at small zoom.
            _navCts = new CancellationTokenSource();
            var navToken = _navCts.Token;
            var iconSize = FileGrid.IconSize;

            for (int off = 0; off < items.Count; off += BatchSize)
            {
                if (navToken.IsCancellationRequested) break;

                var batch = items.Count - off < BatchSize
                    ? items.GetRange(off, items.Count - off)
                    : items.GetRange(off, BatchSize);

                foreach (var item in batch)
                    item.LoadIconAsync(navToken, iconSize);

                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        };

        _navigation.TitleChanged += displayName =>
        {
            Title = displayName;
            TitleBar.SetTitle(displayName);
        };

        _navigation.DirectoryLoaded += path =>
        {
            string displayPath = path == NavigationService.MyComputerPath ? "My Computer" : path;
            NavToolbar.SetPath(displayPath);
        };

        _navigation.StatusChanged += status => StatusBar.SetStatus(status);

        _navigation.NavStateChanged += () =>
            NavToolbar.UpdateNavStates(_navigation.CanGoBack, _navigation.CanGoForward, _navigation.CanGoUp);
    }

    private void WireUpKeybindingEvents()
    {
        _keybinding.F2Pressed += () =>
        {
            var selected = FindFirstSelected();
            if (selected != null)
            {
                _interaction.CommitActiveRename(Folders, FileGrid.FolderItemsControl);
                _interaction.BeginRename(selected, FileGrid.FolderItemsControl);
            }
        };

        _keybinding.DeletePressed += () =>
        {
            var selected = FindFirstSelected();
            if (selected != null)
            {
                _interaction.CommitActiveRename(Folders, FileGrid.FolderItemsControl);
                _interaction.DeleteToTrash(selected);
            }
        };

        _keybinding.F5Pressed += () =>
        {
            if (!string.IsNullOrEmpty(_navigation.CurrentPath))
            {
                _interaction.CommitActiveRename(Folders, FileGrid.FolderItemsControl);
                _navigation.NavigateTo(_navigation.CurrentPath, pushToHistory: false);
            }
        };

        _keybinding.ZoomRequested += zoomIn =>
        {
            if (zoomIn)
                StatusBar.ZoomSlider.Value = Math.Min(StatusBar.ZoomSlider.Maximum, StatusBar.ZoomSlider.Value + 1);
            else
                StatusBar.ZoomSlider.Value = Math.Max(StatusBar.ZoomSlider.Minimum, StatusBar.ZoomSlider.Value - 1);
        };
    }

    private void WireUpInteractionEvents()
    {
        _interaction.NavigateRequested = path => _navigation.NavigateTo(path);

        _interaction.ContextMenuRequested = (screenPos, selectedPaths) =>
        {
            ShellContextMenuService.Show(this, screenPos, selectedPaths);
        };
    }

    private void WireUpUIEvents()
    {
        MenuBar.DeleteRequested += () =>
        {
            var selected = FindFirstSelected();
            if (selected != null)
            {
                _interaction.CommitActiveRename(Folders, FileGrid.FolderItemsControl);
                _interaction.DeleteToTrash(selected);
            }
        };

        MenuBar.RenameRequested += () =>
        {
            var selected = FindFirstSelected();
            if (selected != null)
            {
                _interaction.CommitActiveRename(Folders, FileGrid.FolderItemsControl);
                _interaction.BeginRename(selected, FileGrid.FolderItemsControl);
            }
        };

        FileGrid.MouseWheelPreview += e => _keybinding.HandleMouseWheel(e);

        SidebarPanel.NavigateRequested += path => _navigation.NavigateTo(path);

        NavToolbar.BackRequested       += () => _navigation.GoBack();
        NavToolbar.ForwardRequested    += () => _navigation.GoForward();
        NavToolbar.UpRequested         += () => _navigation.GoUp();
        NavToolbar.NavigateToRequested += path =>
        {
            string target = path.Trim();
            if (string.IsNullOrEmpty(target)) return;

            // Allow typing "::mycomputer" to go back to My Computer
            if (target.Equals("my computer", StringComparison.OrdinalIgnoreCase))
                target = NavigationService.MyComputerPath;

            _navigation.NavigateTo(target);
        };
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        => _keybinding.HandleKeyDown(e);

    /// <summary>
    /// Re-loads all current folder icons at the given zoom size.
    /// Clears old icons and starts a new batch at the new size.
    /// </summary>
    private async Task ReloadIconsAtCurrentZoom(int iconSize)
    {
        _navCts = new CancellationTokenSource();
        var navToken = _navCts.Token;

        // Yield so the UI processes the nulled icons before we start new loads
        await Dispatcher.Yield(DispatcherPriority.Background);

        for (int off = 0; off < Folders.Count; off += BatchSize)
        {
            if (navToken.IsCancellationRequested) break;

            int end = Math.Min(off + BatchSize, Folders.Count);
            for (int i = off; i < end; i++)
                Folders[i].LoadIconAsync(navToken, iconSize);

            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Handles directory change events from the watcher - refreshes the current view.
    /// Guards against re-entrant refreshes while one is already in flight.
    /// </summary>
    private void OnDirectoryChanged()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            if (!string.IsNullOrEmpty(_navigation.CurrentPath))
            {
                _interaction.CommitActiveRename(Folders, FileGrid.FolderItemsControl);
                _navigation.NavigateTo(_navigation.CurrentPath, pushToHistory: false);
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private FolderItem? FindFirstSelected()
    {
        foreach (var item in Folders)
            if (item.IsSelected) return item;
        return null;
    }
}
