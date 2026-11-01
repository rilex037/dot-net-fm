using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;

namespace DotNetFM;

public partial class MainWindow : Window, IAppContextMenuHost
{
    private readonly IModule _module;
    private readonly TabManager _tabs;
    private readonly FileInteractionService _interaction;
    private readonly DragDropService _dragDrop = new();
    private readonly FileGridView _fileGrid = new();

    private readonly ObservableCollection<SidebarSectionView> _sidebarSections = [];

    private readonly string _userProfilePath;
    private TabStore? _subscribedTab;
    private readonly TabStripBuilder _tabStrip;
    private readonly FileViewContainer _activeView;

    private readonly List<BookmarkEntry> _bookmarks = [];
    private SidebarSectionView? _bookmarksSectionView;
    private string? _pendingRenamePath;
    private string? _pendingRenameDir;
    private bool _sidebarCollapsed;
    private double _sidebarWidth = 200;
    private string _lastStatusText = "";
    private PendingSelection? _pendingSelection;
    private System.Windows.Threading.DispatcherTimer? _selectionRetryTimer;
    private int _selectionRetryCount;
    private System.Windows.Threading.DispatcherTimer? _binPollTimer;
    private string _lastBinSignature = "";
    private DateTime _lastVirtualRefreshAt = DateTime.MinValue;

    private readonly record struct PendingSelection(Guid TabId, string Folder, string Target);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ((HwndSource)PresentationSource.FromVisual(this)).AddHook(WndProc);
        UpdateChromeForWindowState();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateChromeForWindowState();
    }

    private void UpdateChromeForWindowState()
    {
        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome == null) return;

        if (WindowState == WindowState.Maximized)
        {
            chrome.ResizeBorderThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
        }
        else
        {
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.CornerRadius = new CornerRadius(10);
        }
    }

    public MainWindow(string initialPath = "")
    {
        InitializeComponent();

        _activeView = FileContainer;
        FileContainer.SetActiveContent(_fileGrid);

        RestoreWindowPlacement();

        IModule module;
        string rawPath;

        if (string.IsNullOrEmpty(initialPath))
        {
            module = App.Modules.DefaultModule;
            rawPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            module = App.Modules.FindByPath(initialPath) ?? App.Modules.DefaultModule;
            rawPath = ModuleUri.Parse(initialPath).Path;

            bool pathExists = module.FileProvider.PathExists(rawPath);
            if (!pathExists)
                rawPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        _module = module;
        _userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _interaction = new FileInteractionService(
            resolveFileOperations: path => App.Modules.FindByPath(path)?.FileOperations,
            resolveFileProvider: path => App.Modules.FindByPath(path)?.FileProvider);
        _tabs = new TabManager(rawPath, module);
        _tabStrip = new TabStripBuilder(module, _tabs);

        SidebarPanel.Sections = _sidebarSections;
        SidebarPanel.FileProvider = _module.FileProvider;

        InitializeSidebar(rawPath);
        WireUpSidebarAndNavEvents();
        WireUpZoomEvents();
        WireUpTabManagerEvents();
        WireUpInteractionEvents();
        WireUpMenuBarEvents();
        LoadBookmarks();

        var savedTabs = TabPersistenceService.LoadTabs();
        if (savedTabs.Count > 0)
        {
            foreach (var saved in savedTabs)
                _tabs.AddTab(saved.Path, saved.SortKey, saved.SortAscending);

            int activeIdx = TabPersistenceService.LoadActiveTabIndex();
            if (activeIdx >= 0 && activeIdx < _tabs.Tabs.Count)
                _tabs.SetActiveTab(activeIdx);
        }
        else
        {
            _tabs.AddTab();
        }

        if (!string.IsNullOrEmpty(initialPath))
        {
            bool found = false;
            foreach (var store in _tabs.Tabs)
            {
                if (string.Equals(store.State.ActivePath, rawPath, StringComparison.OrdinalIgnoreCase))
                {
                    _tabs.SetActiveTab(store.State.TabId);
                    found = true;
                    break;
                }
            }

            if (!found)
                _tabs.AddTab(rawPath);
        }

        _sidebarWidth = Math.Clamp(AppStore.ReadDouble("sidebar.width", 200), 140, 480);
        _sidebarCollapsed = AppStore.ReadBool("sidebar.collapsed", false);
        ApplySidebarState();

        MenuBar.SetBookmarksSectionVisible(_bookmarksSectionView?.IsCollapsed != true);
        MenuBar.SetHiddenFilesShown(AppStore.ReadBool("view.showhidden", false));

        Activated += OnWindowActivatedForVirtualRefresh;

        _binPollTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _binPollTimer.Tick += async (_, _) => await PollRecycleBinSignatureAsync();
        _binPollTimer.Start();
    }

    private void OnWindowActivatedForVirtualRefresh(object? sender, EventArgs e)
    {
        if ((DateTime.UtcNow - _lastVirtualRefreshAt).TotalMilliseconds < 800) return;
        _lastVirtualRefreshAt = DateTime.UtcNow;

        foreach (var tab in _tabs.Tabs)
        {
            string path = tab.State.ActivePath;
            if (path.Length > 0 && _module.FileProvider.IsVirtualRoot(path))
                tab.Dispatch(new TabAction.BeginRefresh());
        }
    }

    private async System.Threading.Tasks.Task PollRecycleBinSignatureAsync()
    {
        bool binOpen = false;
        foreach (var tab in _tabs.Tabs)
        {
            if (RecycleBinService.IsRecycleBin(tab.State.ActivePath))
            {
                binOpen = true;
                break;
            }
        }

        if (!binOpen)
        {
            _lastBinSignature = "";
            return;
        }

        string signature = await System.Threading.Tasks.Task.Run(RecycleBinService.GetBinSignature);

        if (_lastBinSignature.Length == 0)
        {
            _lastBinSignature = signature;
            return;
        }

        if (signature == _lastBinSignature) return;

        _lastBinSignature = signature;
        RefreshVirtualTabs(RecycleBinService.RecycleBinPath);
    }

    public void OpenLocation(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            ActivateWindow();
            return;
        }

        var module = App.Modules.FindByPath(rawPath) ?? App.Modules.DefaultModule;
        string path = ModuleUri.Parse(rawPath).Path;

        if (!module.FileProvider.PathExists(path))
        {
            if (File.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                catch { }
            }
            ActivateWindow();
            return;
        }

        foreach (var store in _tabs.Tabs)
        {
            if (string.Equals(store.State.ActivePath, path, StringComparison.OrdinalIgnoreCase))
            {
                _tabs.SetActiveTab(store.State.TabId);
                ActivateWindow();
                return;
            }
        }

        _tabs.AddTab(path);
        ActivateWindow();
    }

    public void HandleLaunchRequest(LaunchRequest request)
    {
        switch (request.Kind)
        {
            case LaunchRequestKind.Select:
                OpenFolderAndSelect(request.Path);
                break;

            case LaunchRequestKind.Open:
                OpenLocation(request.Path);
                break;

            default:
                ActivateWindow();
                break;
        }
    }

    public void OpenFolderAndSelect(string targetPath)
    {
        string target = targetPath.Trim().Trim('"').Trim();
        if (target.Length == 0)
        {
            ActivateWindow();
            return;
        }

        var module = App.Modules.FindByPath(target) ?? App.Modules.DefaultModule;
        string? folder = module.FileProvider.GetParentPath(target);

        if (string.IsNullOrEmpty(folder) || !module.FileProvider.PathExists(folder))
        {
            OpenLocation(target);
            return;
        }

        TabStore? store = null;
        foreach (var candidate in _tabs.Tabs)
        {
            if (string.Equals(candidate.State.ActivePath, folder, StringComparison.OrdinalIgnoreCase))
            {
                store = candidate;
                break;
            }
        }

        if (store == null)
            store = _tabs.AddTab(folder);
        else
            _tabs.SetActiveTab(store.State.TabId);

        _pendingSelection = new PendingSelection(store.State.TabId, folder, target);
        ActivateWindow();
        TryApplyPendingSelection();
    }

    private void TryApplyPendingSelection()
    {
        var pending = _pendingSelection;
        if (pending == null) return;

        var store = _tabs.ActiveTab;
        if (store == null || store.State.TabId != pending.Value.TabId) return;
        if (store.State.LoadStatus != TabLoadStatus.Idle) return;

        _pendingSelection = null;
        StopSelectionRetry();
        _selectionRetryCount = 0;

        Dispatcher.InvokeAsync(() =>
        {
            if (_tabs.ActiveTab?.State.TabId != pending.Value.TabId) return;
            if (_activeView.SelectItemByPath(pending.Value.Target)) return;
            StartSelectionRetry(pending.Value);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void StartSelectionRetry(PendingSelection pending)
    {
        StopSelectionRetry();

        _selectionRetryTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };

        _selectionRetryTimer.Tick += (_, _) =>
        {
            _selectionRetryCount++;

            if (_selectionRetryCount > 25 || _tabs.ActiveTab?.State.TabId != pending.TabId)
            {
                StopSelectionRetry();
                return;
            }

            if (_activeView.SelectItemByPath(pending.Target))
                StopSelectionRetry();
        };

        _selectionRetryTimer.Start();
    }

    private void StopSelectionRetry()
    {
        _selectionRetryTimer?.Stop();
        _selectionRetryTimer = null;
    }

    private void ActivateWindow()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void RestoreWindowPlacement()
    {
        if (AppStore.ReadBool("window.maximized", false))
        {
            WindowState = WindowState.Maximized;
            return;
        }

        if (AppStore.Read("window.placed") != "1")
            return;

        double left = AppStore.ReadDouble("window.left", double.NaN);
        double top = AppStore.ReadDouble("window.top", double.NaN);
        double width = AppStore.ReadDouble("window.width", double.NaN);
        double height = AppStore.ReadDouble("window.height", double.NaN);

        if (double.IsNaN(left) || double.IsNaN(top) || double.IsNaN(width) || double.IsNaN(height) ||
            width < 400 || height < 300)
            return;

        double vx = SystemParameters.VirtualScreenLeft;
        double vy = SystemParameters.VirtualScreenTop;
        double vw = SystemParameters.VirtualScreenWidth;
        double vh = SystemParameters.VirtualScreenHeight;

        bool visible = left >= vx - width + 120 &&
                       left <= vx + vw - 120 &&
                       top >= vy - 10 &&
                       top <= vy + vh - 40;
        if (!visible) return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        StopSelectionRetry();

        bool maximized = WindowState == WindowState.Maximized;

        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["window.maximized"] = maximized ? "1" : "0",
            ["window.placed"] = "1"
        };

        if (!maximized)
        {
            changes["window.left"] = ((int)Left).ToString();
            changes["window.top"] = ((int)Top).ToString();
            changes["window.width"] = ((int)Width).ToString();
            changes["window.height"] = ((int)Height).ToString();
        }

        if (_tabs.ActiveTab != null)
            changes["tab.iconsize"] = _tabs.ActiveTab.State.IconSize.ToString();

        if (!_sidebarCollapsed && SidebarColumn.ActualWidth > 10)
            changes["sidebar.width"] = ((int)SidebarColumn.ActualWidth).ToString();

        AppStore.WriteMany(changes);

        TabPersistenceService.SaveTabs(_tabs.Tabs, _tabs.ActiveTab);
    }

    private void InitializeSidebar(string initialPath)
    {
        SidebarIconProvider.Initialize(SidebarService.Load().SidebarIcons);

        var module = App.Modules.FindByPath(initialPath);
        var sections = module?.GetSidebarSections()
            .OrderBy(s => s.Order)
            .ToList();

        if (sections != null)
        {
            foreach (var section in sections)
            {
                var view = new SidebarSectionView
                {
                    Id = section.Id,
                    Title = section.Title
                };

                foreach (var entry in section.Entries)
                {
                    string resolvedPath = entry.Path;
                    string displayName = entry.Name;

                    if (entry.Icon.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                        resolvedPath.Equals(_userProfilePath, StringComparison.OrdinalIgnoreCase))
                        displayName = Environment.UserName;

                    view.Items.Add(new SidebarItem.Item
                    {
                        Name = displayName,
                        IconPath = SidebarIconProvider.GetIconPath(entry.Icon),
                        Path = resolvedPath,
                    });
                }

                _sidebarSections.Add(view);
            }
        }

        _bookmarksSectionView = new SidebarSectionView { Id = "bookmarks", Title = "Bookmarks" };
        _sidebarSections.Add(_bookmarksSectionView);
        SidebarPanel.BookmarksSection = _bookmarksSectionView;
        RefreshBookmarksSection();

        foreach (var view in _sidebarSections)
            view.RestoreCollapsedState();
    }

    private void WireUpTabManagerEvents()
    {
        _tabs.ActiveTabChanged += OnActiveTabChanged;
        _tabs.AllTabsClosed += () => _tabs.AddTab();
        _tabs.TabClosed += _ => _tabStrip.Rebuild(TabStripPanel);
    }

    private void OnActiveTabChanged(TabStore? newTab)
    {
        if (newTab == null) return;

        if (_pendingSelection != null && newTab.State.TabId != _pendingSelection.Value.TabId)
            _pendingSelection = null;

        if (_subscribedTab != null)
            _subscribedTab.StateChanged -= OnActiveTabStateChanged;

        _subscribedTab = newTab;
        newTab.StateChanged += OnActiveTabStateChanged;

        _activeView.Folders = newTab.Folders;
        _activeView.InteractionService = _interaction;
        _activeView.DragDropService = _dragDrop;
        _activeView.CurrentPath = newTab.State.ActivePath;
        _activeView.IconSize = newTab.State.IconSize;

        StatusBar.SetZoomForIconSize(newTab.State.IconSize);
        ApplyStateToUI(newTab.State);
        _tabStrip.Rebuild(TabStripPanel);
    }

    private void OnActiveTabStateChanged(TabStateRecord state)
    {
        if (_tabs.ActiveTab?.State.TabId != state.TabId) return;

        var store = _tabs.ActiveTab;
        store.SaveScrollOffset(_activeView.CurrentPath, _activeView.VerticalOffset);

        double savedOffset = store.GetScrollOffset(state.ActivePath);
        ApplyStateToUI(state);
        _activeView.ResetScroll(savedOffset);
        TryBeginPendingRename(store);

        if (_pendingSelection == null) return;

        if (string.Equals(state.ActivePath, _pendingSelection.Value.Folder, StringComparison.OrdinalIgnoreCase))
            TryApplyPendingSelection();
        else
            _pendingSelection = null;
    }

    private void ApplyStateToUI(TabStateRecord state)
    {
        string displayTitle = _module.FileProvider.GetDisplayTitle(state.ActivePath);
        Title = displayTitle;
        TitleBar.SetTitle(displayTitle);
        NavToolbar.SetPath(_module.FileProvider.GetDisplayPath(state.ActivePath));
        NavToolbar.UpdateNavStates(state.CanGoBack, state.CanGoForward, state.CanGoUp);
        StatusBar.SetStatus(state.StatusText);
        _lastStatusText = state.StatusText;
        _activeView.IconSize = state.IconSize;
        _activeView.CurrentPath = state.ActivePath;
    }

    private void WireUpInteractionEvents()
    {
        _interaction.NavigateRequested = path =>
        {
            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.NavigateTo(path));
        };

        _interaction.ContextMenuRequested = (screenPos, selectedPaths) =>
        {
            if (selectedPaths.Count > 0)
                AppContextMenuService.ShowSelection(this, screenPos, selectedPaths, _tabs.ActiveTab?.State.ActivePath, this);
        };

        _interaction.ErrorDisplayRequested = message =>
        {
            CustomDialog.Show("Error", message, DialogIcon.Error);
        };

        _interaction.ConflictResolutionRequested = () =>
        {
            var choice = CustomDialog.Show(
                "Confirm Replace",
                "This location already contains items with the same name.\n\n" +
                "Replace the existing items?\n\n" +
                "Yes = replace, No = keep existing items and copy the rest, Cancel = do nothing.",
                DialogIcon.Confirmation,
                DialogButtons.YesNoCancel);

            return choice switch
            {
                CustomDialogResult.Yes => IFileOperations.ConflictPolicy.Overwrite,
                CustomDialogResult.No => IFileOperations.ConflictPolicy.Skip,
                _ => IFileOperations.ConflictPolicy.Cancel,
            };
        };

        _interaction.RenameManager.RenameReady += item =>
        {
            _interaction.RenameManager.BeginRename(item);
            _activeView.FocusRenameTextBox(item);
        };

        _dragDrop.TransferRequested = (paths, targetDir, forceCopy) =>
        {
            _interaction.HandleDroppedFiles(paths, targetDir, forceCopy);
        };

        _interaction.TransferCompleted = (sources, targetDir) =>
        {
            var folders = new List<string>();
            foreach (string source in sources)
            {
                string? dir = Path.GetDirectoryName(source);
                if (dir != null) folders.Add(dir);
            }

            folders.Add(targetDir);
            RefreshTabsForFolders(folders);
        };

        _activeView.OpenInNewTabRequested += path => _tabs.AddTab(path);

        _activeView.DropOnFileRequested += _interaction.HandleDropOnFile;

        _activeView.BackgroundContextMenuRequested += (screenPos, path) =>
        {
            bool isVirtual = string.IsNullOrEmpty(path) ||
                (App.Modules.FindByPath(path)?.FileProvider.IsVirtualRoot(path) ?? true);
            AppContextMenuService.ShowBackground(this, screenPos, path, isVirtual, this);
        };

        _activeView.SelectionChanged += UpdateSelectionStatus;
    }

    private void WireUpSidebarAndNavEvents()
    {
        SidebarPanel.NavigateRequested += path =>
        {
            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.NavigateTo(path));
        };

        SidebarPanel.OpenInNewTabRequested += path => _tabs.AddTab(path);

        SidebarPanel.BookmarkContextMenuRequested += OnSidebarBookmarkContextMenuRequested;
        SidebarPanel.BookmarksSectionContextMenuRequested += _ =>
            AppContextMenuService.ShowForBookmarksSection(this, AddBookmarkForCurrentLocation, ManageBookmarks);
        SidebarPanel.BookmarksDropped += OnBookmarksDropped;

        NavToolbar.BackRequested += () =>
        {
            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.GoBack());
        };

        NavToolbar.ForwardRequested += () =>
        {
            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.GoForward());
        };

        NavToolbar.UpRequested += () =>
        {
            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.GoUp());
        };

        NavToolbar.NavigateCancelled += () =>
        {
            var state = _tabs.ActiveTab?.State;
            if (state != null)
                NavToolbar.SetPath(_module.FileProvider.GetDisplayPath(state.ActivePath));
            _activeView.Focus();
        };

        NavToolbar.NavigateToRequested += path =>
        {
            string target = path.Trim();
            if (string.IsNullOrEmpty(target)) return;

            target = _module.ResolveDisplayName(target) ?? target;

            string previousPath = _tabs.ActiveTab?.State.ActivePath ?? "";

            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.NavigateTo(target));

            if (_tabs.ActiveTab != null && _tabs.ActiveTab.State.ActivePath != target)
            {
                NavToolbar.SetPath(_module.FileProvider.GetDisplayPath(previousPath));
                CustomDialog.Show(
                    "Navigation Error",
                    $"Could not find \u201c{path}\u201d.\nPlease check the spelling and try again.",
                    DialogIcon.Error);
            }
        };

        StatusBar.SidebarToggleRequested += ToggleSidebar;
    }

    private void WireUpZoomEvents()
    {
        _activeView.MouseWheelPreview += e =>
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Delta > 0)
                    StatusBar.ZoomSlider.Value = Math.Min(StatusBar.ZoomSlider.Maximum, StatusBar.ZoomSlider.Value + 1);
                else
                    StatusBar.ZoomSlider.Value = Math.Max(StatusBar.ZoomSlider.Minimum, StatusBar.ZoomSlider.Value - 1);
                e.Handled = true;
            }
        };

        StatusBar.ZoomChanged += size =>
        {
            if (_tabs.ActiveTab == null) return;
            _tabs.DispatchActive(new TabAction.SetIconSize(size));
        };
    }

    private void WireUpMenuBarEvents()
    {
        MenuBar.AddBookmarkRequested += AddBookmarkForCurrentLocation;
        MenuBar.ManageBookmarksRequested += ManageBookmarks;
        MenuBar.BookmarksSectionToggleRequested += ToggleBookmarksSection;
        MenuBar.HiddenFilesToggleRequested += ToggleHiddenFiles;
        MenuBar.SettingsRequested += ShowSettingsDialog;
    }

    private void LoadBookmarks()
    {
        _bookmarks.Clear();
        _bookmarks.AddRange(BookmarkService.Load());
        RefreshBookmarksSection();
    }

    private void SaveBookmarks()
    {
        BookmarkService.Save(_bookmarks);
        RefreshBookmarksSection();
    }

    private void RefreshBookmarksSection()
    {
        if (_bookmarksSectionView == null) return;

        _bookmarksSectionView.Items.Clear();

        if (_bookmarks.Count == 0)
        {
            _bookmarksSectionView.Items.Add(new SidebarItem.Item
            {
                Name = "Drag folders here to bookmark them",
                IconPath = SidebarIconProvider.GetIconPath("Bookmark"),
                Path = "",
                IsHint = true
            });
            return;
        }

        foreach (var bookmark in _bookmarks)
        {
            _bookmarksSectionView.Items.Add(new SidebarItem.Item
            {
                Name = bookmark.Name,
                IconPath = SidebarIconProvider.GetIconPath("Folder"),
                Path = bookmark.Path,
                Tag = bookmark
            });
        }
    }

    private void OnSidebarBookmarkContextMenuRequested(SidebarItem.Item item, Point screenPos)
    {
        if (item.Tag is not BookmarkEntry entry) return;

        AppContextMenuService.ShowForBookmark(
            this,
            entry,
            () =>
            {
                _tabs.CommitActiveRename(_activeView);
                _tabs.DispatchActive(new TabAction.NavigateTo(entry.Path));
            },
            () => _tabs.AddTab(entry.Path),
            () => OnBookmarkRenameRequested(entry),
            () => OnBookmarkRemoveRequested(entry));
    }

    private void AddBookmarkForCurrentLocation()
    {
        var tab = _tabs.ActiveTab;
        string path = tab?.State.ActivePath ?? "";
        if (string.IsNullOrEmpty(path)) return;

        var provider = App.Modules.FindByPath(path)?.FileProvider;
        if (provider?.IsVirtualRoot(path) == true) return;

        string suggested = BookmarkService.SuggestName(path);
        string? name = InputDialog.Show("Add Bookmark", "Bookmark name:", suggested);
        if (string.IsNullOrWhiteSpace(name)) return;

        _bookmarks.Add(new BookmarkEntry(name.Trim(), path));
        SaveBookmarks();
    }

    private void OnBookmarkRenameRequested(BookmarkEntry entry)
    {
        int index = _bookmarks.IndexOf(entry);
        if (index < 0) return;

        string? name = InputDialog.Show("Rename Bookmark", "Bookmark name:", entry.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        _bookmarks[index] = new BookmarkEntry(name.Trim(), entry.Path);
        SaveBookmarks();
    }

    private void OnBookmarkRemoveRequested(BookmarkEntry entry)
    {
        _bookmarks.Remove(entry);
        SaveBookmarks();
    }

    private void OnBookmarksDropped(IReadOnlyList<string> paths)
    {
        bool changed = false;
        foreach (var path in paths)
        {
            if (_bookmarks.Any(b => string.Equals(b.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            _bookmarks.Add(new BookmarkEntry(BookmarkService.SuggestName(path), path));
            changed = true;
        }

        if (changed)
            SaveBookmarks();
    }

    private void ManageBookmarks()
    {
        var result = BookmarkManagerDialog.Show(_bookmarks);
        if (result == null) return;

        _bookmarks.Clear();
        _bookmarks.AddRange(result);
        SaveBookmarks();
    }

    private void ToggleBookmarksSection()
    {
        if (_bookmarksSectionView == null) return;

        _bookmarksSectionView.ToggleCollapsed();
        MenuBar.SetBookmarksSectionVisible(!_bookmarksSectionView.IsCollapsed);
    }

    public void ToggleHiddenFiles()
    {
        bool show = !AppStore.ReadBool("view.showhidden", false);
        AppStore.Write("view.showhidden", show ? "1" : "0");
        MenuBar.SetHiddenFilesShown(show);

        foreach (var tab in _tabs.Tabs)
            tab.Dispatch(new TabAction.BeginRefresh());
    }

    private void ToggleSidebar()
    {
        if (!_sidebarCollapsed && SidebarColumn.ActualWidth > 10)
            _sidebarWidth = SidebarColumn.ActualWidth;

        _sidebarCollapsed = !_sidebarCollapsed;
        AppStore.Write("sidebar.collapsed", _sidebarCollapsed ? "1" : "0");
        ApplySidebarState();
    }

    private void ApplySidebarState()
    {
        if (_sidebarCollapsed)
        {
            SidebarColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
        }
        else
        {
            SidebarColumn.Width = new GridLength(_sidebarWidth);
            SplitterColumn.Width = new GridLength(5);
        }

        StatusBar.SetSidebarCollapsed(_sidebarCollapsed);
    }

    public void OpenPaths(IReadOnlyList<string> paths)
    {
        bool navigated = false;

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                if (navigated) continue;
                _tabs.CommitActiveRename(_activeView);
                _tabs.DispatchActive(new TabAction.NavigateTo(path));
                navigated = true;
            }
            else if (File.Exists(path))
            {
                var result = _interaction.ProcessLauncher.OpenWithShell(path);
                if (!result.Success && result.ErrorMessage != null)
                    _interaction.ErrorDisplayRequested?.Invoke(result.ErrorMessage);
            }
        }
    }

    public void OpenPathInNewTab(string path) => _tabs.AddTab(path);

    public void CutPaths(IReadOnlyList<string> paths) => _interaction.Clipboard.Cut(paths);

    public void CopyPaths(IReadOnlyList<string> paths) => _interaction.Clipboard.Copy(paths);

    public void PasteInto(string targetDirectory) => _interaction.HandlePaste(targetDirectory);

    public void ExtractArchive(string archivePath, ArchiveExtractMode mode) =>
        ArchiveUi.ExtractArchive(this, archivePath, mode, RefreshTabsForFolders);

    public void CompressItems(IReadOnlyList<string> paths, string? formatId) =>
        ArchiveUi.Compress(this, paths, formatId, RefreshTabsForFolders);

    private void RefreshTabsForFolders(IEnumerable<string> folders)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string folder in folders)
        {
            if (folder.Length == 0) continue;
            if (Directory.Exists(folder) || folder.StartsWith("::", StringComparison.Ordinal))
                targets.Add(folder);
        }

        if (targets.Count == 0) return;

        foreach (var tab in _tabs.Tabs)
        {
            if (targets.Contains(tab.State.ActivePath))
                tab.Dispatch(new TabAction.BeginRefresh());
        }
    }

    public void RenameSelection() => BeginRenameSingleSelected();

    public void DeletePaths(IReadOnlyList<string> paths) => _interaction.DeletePathsToTrash(paths);

    public void RestoreFromRecycleBin(IReadOnlyList<string> paths) => RunRecycleBinRestore(paths);

    public void DeleteFromRecycleBin(IReadOnlyList<string> paths) => RunRecycleBinDelete(paths);

    private List<RecycleBinService.RecycleEntry> ResolveRecycleEntries(IReadOnlyList<string> paths)
    {
        var entries = new List<RecycleBinService.RecycleEntry>(paths.Count);

        var byPath = new Dictionary<string, FolderItem>(StringComparer.OrdinalIgnoreCase);
        var activeTab = _tabs.ActiveTab;
        if (activeTab != null)
        {
            foreach (var item in activeTab.Folders)
                byPath[item.FullPath] = item;
        }

        foreach (string path in paths)
        {
            if (byPath.TryGetValue(path, out var item))
                entries.Add(new RecycleBinService.RecycleEntry(item.Name, item.FullPath));
            else
                entries.Add(new RecycleBinService.RecycleEntry(Path.GetFileName(path), path));
        }

        return entries;
    }

    private void RunRecycleBinRestore(IReadOnlyList<string> paths)
    {
        var entries = ResolveRecycleEntries(paths);
        if (entries.Count == 0) return;

        var restoredFolders = new List<string>();
        var errors = RecycleBinService.Restore(entries, restoredFolders);

        if (errors.Count > 0)
            CustomDialog.Show("Restore", string.Join("\n", errors), DialogIcon.Error);

        var folders = new List<string>(restoredFolders) { RecycleBinService.RecycleBinPath };
        RefreshTabsForFolders(folders);
        RefreshVirtualTabs(RecycleBinService.RecycleBinPath, 400, 1200);
    }

    private void RunRecycleBinDelete(IReadOnlyList<string> paths)
    {
        var entries = ResolveRecycleEntries(paths);
        if (entries.Count == 0) return;

        string countText = entries.Count == 1
            ? $"\u201c{entries[0].Name}\u201d"
            : $"these {entries.Count} items";

        var choice = CustomDialog.Show(
            "Delete Permanently",
            $"Are you sure you want to permanently delete {countText}?\n\n" +
            "Items deleted from the Recycle Bin cannot be restored.",
            DialogIcon.Confirmation,
            DialogButtons.YesNo);

        if (choice != CustomDialogResult.Yes) return;

        var errors = RecycleBinService.DeletePermanently(entries);
        if (errors.Count > 0)
            CustomDialog.Show("Delete Permanently", string.Join("\n", errors), DialogIcon.Error);

        RefreshVirtualTabs(RecycleBinService.RecycleBinPath, 400, 1200);
    }

    private void RefreshVirtualTabs(string path, params int[] delaysInMs)
    {
        if (string.IsNullOrEmpty(path)) return;

        var tabIds = new List<Guid>();
        foreach (var tab in _tabs.Tabs)
        {
            if (tab.State.ActivePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                tabIds.Add(tab.State.TabId);
        }

        if (tabIds.Count == 0) return;

        foreach (Guid tabId in tabIds)
            ScheduleTabRefresh(tabId, TimeSpan.Zero);

        foreach (int delay in delaysInMs)
            foreach (Guid tabId in tabIds)
                ScheduleTabRefresh(tabId, TimeSpan.FromMilliseconds(delay));
    }

    private void ScheduleActiveFolderRefresh(params int[] delaysInMs)
    {
        var tab = _tabs.ActiveTab;
        if (tab == null) return;

        string path = tab.State.ActivePath;
        if (string.IsNullOrEmpty(path) || !_module.FileProvider.IsVirtualRoot(path)) return;

        foreach (var other in _tabs.Tabs)
        {
            if (!other.State.ActivePath.Equals(path, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (int delay in delaysInMs)
                ScheduleTabRefresh(other.State.TabId, TimeSpan.FromMilliseconds(delay));
        }
    }

    private void ScheduleTabRefresh(Guid tabId, TimeSpan delay)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            TabStore? store = null;
            foreach (var t in _tabs.Tabs)
            {
                if (t.State.TabId == tabId)
                {
                    store = t;
                    break;
                }
            }

            if (store == null) return;
            if (!_module.FileProvider.IsVirtualRoot(store.State.ActivePath)) return;

            store.Dispatch(new TabAction.BeginRefresh());
        };
        timer.Start();
    }

    public void CopyPathsAsText(IReadOnlyList<string> paths)
    {
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, paths.Select(p => $"\"{p}\"")));
        }
        catch
        {
        }
    }

    public void CreateNewFolder(string targetDirectory) =>
        CreateNewItem(targetDirectory, "New Folder", "", isFolder: true);

    public void CreateNewTextDocument(string targetDirectory) =>
        CreateNewItem(targetDirectory, "New Text Document", ".txt", isFolder: false);

    private void CreateNewItem(string targetDirectory, string baseName, string extension, bool isFolder)
    {
        if (string.IsNullOrEmpty(targetDirectory)) return;

        try
        {
            string name = GetUniqueChildName(targetDirectory, baseName, extension);
            string path = Path.Combine(targetDirectory, name);

            if (isFolder)
                Directory.CreateDirectory(path);
            else
                File.WriteAllText(path, "");

            BeginRenameAfterReload(path, targetDirectory);
        }
        catch (Exception ex)
        {
            CustomDialog.Show(
                "New Item",
                $"Could not create \u201c{baseName}{extension}\u201d:\n{ex.Message}",
                DialogIcon.Error);
        }
    }

    private static string GetUniqueChildName(string directory, string baseName, string extension)
    {
        string candidate = baseName + extension;
        int counter = 1;

        while (File.Exists(Path.Combine(directory, candidate)) ||
               Directory.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{baseName} - {counter++}{extension}";
        }

        return candidate;
    }

    private void BeginRenameAfterReload(string path, string directory)
    {
        _pendingRenamePath = path;
        _pendingRenameDir = directory;

        _tabs.CommitActiveRename(_activeView);
        _tabs.DispatchActive(new TabAction.BeginRefresh());
    }

    private void TryBeginPendingRename(TabStore tab)
    {
        if (_pendingRenamePath == null || _pendingRenameDir == null) return;
        if (!string.Equals(tab.State.ActivePath, _pendingRenameDir, StringComparison.OrdinalIgnoreCase)) return;

        string pendingPath = _pendingRenamePath;
        _pendingRenamePath = null;
        _pendingRenameDir = null;

        foreach (var item in tab.Folders)
        {
            if (!string.Equals(item.FullPath, pendingPath, StringComparison.OrdinalIgnoreCase)) continue;

            _activeView.ClearAllSelections();
            item.IsSelected = true;
            UpdateSelectionStatus();
            _interaction.BeginRename(item);
            _activeView.FocusRenameTextBox(item);
            return;
        }
    }

    public void OpenTerminal(string targetDirectory) => AppContextMenuService.LaunchTerminal(targetDirectory);

    public void ShowProperties(string path) => AppContextMenuService.ShowShellProperties(this, path);

    public void RefreshView()
    {
        _tabs.CommitActiveRename(_activeView);
        _tabs.DispatchActive(new TabAction.BeginRefresh());
    }

    public void ShowShellMenuForPaths(Point screenPoint, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        var provider = (App.Modules.FindByPath(paths[0]) ?? _module).ContextMenuProvider;
        provider?.Show(this, screenPoint, paths);

        ScheduleActiveFolderRefresh(150, 600, 1500);
    }

    public void ShowShellMenuForDirectory(Point screenPoint, string directory)
    {
        var provider = (App.Modules.FindByPath(directory) ?? _module).ContextMenuProvider;
        provider?.ShowBackground(this, screenPoint, directory);

        ScheduleActiveFolderRefresh(150, 600, 1500);
    }

    public void ApplySort(string sortKey, bool ascending)
    {
        _tabs.CommitActiveRename(_activeView);
        _tabs.DispatchActive(new TabAction.SetSort(sortKey, ascending));
    }

    public string CurrentSortKey => _tabs.ActiveTab?.State.SortKey ?? "name";

    public bool CurrentSortAscending => _tabs.ActiveTab?.State.SortAscending ?? true;

    public bool HiddenFilesShown => AppStore.ReadBool("view.showhidden", false);

    public bool CanPaste() => _interaction.Clipboard.TryGetFileDropList() is { Count: > 0 };

    private void ShowSettingsDialog() => SettingsDialog.Present();

    private void UpdateSelectionStatus()
    {
        var tab = _tabs.ActiveTab;
        if (tab == null) return;

        int total = tab.Folders.Count;
        int selected = 0;
        foreach (var item in tab.Folders)
        {
            if (item.IsSelected) selected++;
        }

        StatusBar.SetStatus(selected > 0 ? $"{selected} of {total} selected" : _lastStatusText);
    }

    public void SelectAllItems()
    {
        var tab = _tabs.ActiveTab;
        if (tab == null) return;

        foreach (var item in tab.Folders)
            item.IsSelected = true;

        UpdateSelectionStatus();
    }

    private void Exit_Executed(object sender, ExecutedRoutedEventArgs e) =>
        Close();

    private void Rename_Executed(object sender, ExecutedRoutedEventArgs e) =>
        BeginRenameSingleSelected();

    private void BeginRenameSingleSelected()
    {
        if (_tabs.ActiveTab == null) return;

        FolderItem? selected = null;
        int count = 0;
        foreach (var item in _tabs.ActiveTab.Folders)
        {
            if (!item.IsSelected) continue;
            count++;
            if (count == 1)
                selected = item;
            if (count > 1)
            {
                selected = null;
                break;
            }
        }

        if (selected == null) return;

        _tabs.CommitActiveRename(_activeView);
        _interaction.BeginRename(selected);
        _activeView.FocusRenameTextBox(selected);
    }

    private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tab = _tabs.ActiveTab;
        if (tab == null) return;

        _tabs.CommitActiveRename(_activeView);

        var selected = tab.Folders.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;

        if (RecycleBinService.IsRecycleBin(tab.State.ActivePath))
        {
            RunRecycleBinDelete(selected.Select(i => i.FullPath).ToList());
            return;
        }

        foreach (var item in selected)
            _interaction.DeleteToTrash(item);
    }

    private void SelectAll_Executed(object sender, ExecutedRoutedEventArgs e) =>
        SelectAllItems();

    private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tab = _tabs.ActiveTab;
        if (tab == null) return;

        if (RecycleBinService.IsRecycleBin(tab.State.ActivePath))
        {
            StatusBar.SetStatus("Copy is not available in the Recycle Bin");
            return;
        }

        _interaction.HandleCopy(tab.Folders);
    }

    private void Cut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tab = _tabs.ActiveTab;
        if (tab == null) return;

        if (RecycleBinService.IsRecycleBin(tab.State.ActivePath))
        {
            StatusBar.SetStatus("Cut is not available in the Recycle Bin");
            return;
        }

        _interaction.HandleCut(tab.Folders);
    }

    private void Paste_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.ActiveTab != null)
            _interaction.HandlePaste(_tabs.ActiveTab.State.ActivePath);
    }

    private void Refresh_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _tabs.CommitActiveRename(_activeView);
        _tabs.DispatchActive(new TabAction.BeginRefresh());
    }

    private void ZoomIn_Executed(object sender, ExecutedRoutedEventArgs e) =>
        StatusBar.ZoomSlider.Value = Math.Min(StatusBar.ZoomSlider.Maximum, StatusBar.ZoomSlider.Value + 1);

    private void ZoomOut_Executed(object sender, ExecutedRoutedEventArgs e) =>
        StatusBar.ZoomSlider.Value = Math.Max(StatusBar.ZoomSlider.Minimum, StatusBar.ZoomSlider.Value - 1);

    private void NewTab_Executed(object sender, ExecutedRoutedEventArgs e) =>
        _tabs.AddTab();

    private void CloseTab_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.ActiveTab != null)
            _tabs.CloseTab(_tabs.ActiveTab.State.TabId);
    }

    private void CycleTab(int delta)
    {
        int count = _tabs.Tabs.Count;
        if (count == 0) return;

        int current = 0;
        var activeId = _tabs.ActiveTab?.State.TabId;
        for (int i = 0; i < count; i++)
        {
            if (_tabs.Tabs[i].State.TabId == activeId)
            {
                current = i;
                break;
            }
        }

        int next = (current + delta + count) % count;
        _tabs.SetActiveTab(next);
    }

    private void NextTab_Executed(object sender, ExecutedRoutedEventArgs e) => CycleTab(1);

    private void PrevTab_Executed(object sender, ExecutedRoutedEventArgs e) => CycleTab(-1);

    private void GoBack_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _tabs.CommitActiveRename(_activeView);
        _tabs.DispatchActive(new TabAction.GoBack());
    }

    private void GoForward_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _tabs.CommitActiveRename(_activeView);
        _tabs.DispatchActive(new TabAction.GoForward());
    }

    private void GoUp_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _tabs.CommitActiveRename(_activeView);
        _tabs.DispatchActive(new TabAction.GoUp());
    }

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var tab = _tabs.ActiveTab;
        if (tab == null) return;

        foreach (var item in tab.Folders)
        {
            if (!item.IsSelected) continue;
            _tabs.CommitActiveRename(_activeView);
            _interaction.OpenItem(item);
            break;
        }
    }

    private void ClearSelection_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        _activeView.ClearAllSelections();
        UpdateSelectionStatus();
    }

    private void FocusAddress_Executed(object sender, ExecutedRoutedEventArgs e) =>
        NavToolbar.FocusAddressBar();

    private void AddBookmark_Executed(object sender, ExecutedRoutedEventArgs e) =>
        AddBookmarkForCurrentLocation();

    private void ToggleBookmarksSection_Executed(object sender, ExecutedRoutedEventArgs e) =>
        ToggleBookmarksSection();

    private void ToggleHiddenFiles_Executed(object sender, ExecutedRoutedEventArgs e) =>
        ToggleHiddenFiles();

    private void AddTabButton_Click(object sender, RoutedEventArgs e) =>
        _tabs.AddTab();

    private void TabStripArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            _tabs.AddTab();
            e.Handled = true;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_GETMINMAXINFO)
            NativeMethods.ConstrainWindowToWorkArea(hwnd, lParam, ref handled);

        return IntPtr.Zero;
    }
}
