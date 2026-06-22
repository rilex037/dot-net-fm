using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;

namespace DotNetFM;

/// <summary>
/// Main window: thin layout shell that delegates all state to
/// <see cref="TabManager"/>. UI updates flow from a single
/// <see cref="TabStore.StateChanged"/> event per tab.
/// </summary>
public partial class MainWindow : Window
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 2u;

    private readonly IModule _module;
    private readonly TabManager _tabs;
    private readonly FileInteractionService _interaction;
    private readonly DragDropService _dragDrop = new();
    private readonly FileGridView _fileGrid = new();

    private ObservableCollection<SidebarSectionView> _sidebarSections = new();

    private readonly string _userProfilePath;
    private TabStore? _subscribedTab;
    private readonly TabStripBuilder _tabStrip;
    private readonly IFileView _activeView;

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
            chrome.CornerRadius = new CornerRadius(20);
        }
    }

    public MainWindow(string initialPath = "")
    {
        InitializeComponent();

        _activeView = FileContainer;
        FileContainer.SetActiveContent(_fileGrid);

        // Restore window geometry from store.
        Left   = double.Parse(AppStore.Read("window.left"));
        Top    = double.Parse(AppStore.Read("window.top"));
        Width  = double.Parse(AppStore.Read("window.width"));
        Height = double.Parse(AppStore.Read("window.height"));

        // Resolve module and path from the raw argument.
        IModule module;
        string rawPath;

        if (string.IsNullOrEmpty(initialPath))
        {
            // No CLI arg — use default module (Windows) with user profile as starting path
            module = App.Modules.DefaultModule;
            rawPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else
        {
            // Try URI prefix lookup first, match against registered prefixes
            module = App.Modules.FindByPath(initialPath) ?? App.Modules.DefaultModule;
            rawPath = ModuleUri.Parse(initialPath).Path;

            // Validate shell-provided path — fall back to user profile if it doesn't exist
            bool pathExists = module.FileProvider.PathExists(rawPath);
            if (!pathExists)
                rawPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        _module = module;
        _userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _interaction = new FileInteractionService(resolveFileOperations: path => App.Modules.FindByPath(path)?.FileOperations);
        _tabs = new TabManager(rawPath, module);
        _tabStrip = new TabStripBuilder(module, _tabs);

        SidebarPanel.Sections = _sidebarSections;
        SidebarPanel.FileProvider = _module.FileProvider;

        InitializeSidebar(rawPath);
        WireUpSidebarAndNavEvents();
        WireUpZoomEvents();
        WireUpTabManagerEvents();
        WireUpInteractionEvents();

        // Restore previously open tabs, or create a default one.
        var savedPaths = TabPersistenceService.LoadTabPaths();
        if (savedPaths.Count > 0)
        {
            foreach (var path in savedPaths)
                _tabs.AddTab(path);

            int activeIdx = TabPersistenceService.LoadActiveTabIndex();
            if (activeIdx >= 0 && activeIdx < _tabs.Tabs.Count)
                _tabs.SetActiveTab(activeIdx);
        }
        else
        {
            _tabs.AddTab();
        }

        // If launched from the shell with a path, activate it if already open, otherwise open new.
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
    }

    // ── Persist state on close ─────────────────────────────────────

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        AppStore.Write("window.left",   Left.ToString());
        AppStore.Write("window.top",    Top.ToString());
        AppStore.Write("window.width",  Width.ToString());
        AppStore.Write("window.height", Height.ToString());

        if (_tabs.ActiveTab != null)
            AppStore.Write("tab.iconsize", _tabs.ActiveTab.State.IconSize.ToString());

        TabPersistenceService.SaveTabs(_tabs.Tabs, _tabs.ActiveTab);
    }

    // ── Sidebar initialization ─────────────────────────────────────

    private void InitializeSidebar(string initialPath)
    {
        SidebarIconMapper.Initialize(SidebarService.Load().SidebarIcons);

        // Use module-contributed sidebar sections — no hardcoded section names
        var module = App.Modules.FindByPath(initialPath);
        var sections = module?.GetSidebarSections()
            .OrderBy(s => s.Order)
            .ToList();

        if (sections != null)
        {
            foreach (var section in sections)
            {
                var view = new SidebarSectionView { Title = section.Title };

                foreach (var entry in section.Entries)
                {
                    string resolvedPath = entry.Path;
                    string displayName = entry.Name;

                    // "Home" entry shows the user's name when it points to the user profile directory
                    if (entry.Icon.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                        resolvedPath.Equals(_userProfilePath, StringComparison.OrdinalIgnoreCase))
                        displayName = Environment.UserName;

                    view.Items.Add(new SidebarItem.Item
                    {
                        Name = displayName,
                        IconPath = SidebarIconMapper.GetIconPath(entry.Icon),
                        Path = resolvedPath,
                    });
                }

                _sidebarSections.Add(view);
            }
        }
    }

    // ── Tab Manager wiring ─────────────────────────────────────────

    private void WireUpTabManagerEvents()
    {
        _tabs.ActiveTabChanged += OnActiveTabChanged;
        _tabs.AllTabsClosed += () => _tabs.AddTab();
        _tabs.TabClosed += _ => _tabStrip.Rebuild(TabStripPanel);
    }

    private void OnActiveTabChanged(TabStore? newTab)
    {
        if (newTab == null) return;

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
    }

    private void ApplyStateToUI(TabStateRecord state)
    {
        string displayTitle = _module.FileProvider.GetDisplayTitle(state.ActivePath);
        Title = displayTitle;
        TitleBar.SetTitle(displayTitle);
        NavToolbar.SetPath(_module.FileProvider.GetDisplayPath(state.ActivePath));
        NavToolbar.UpdateNavStates(state.CanGoBack, state.CanGoForward, state.CanGoUp);
        StatusBar.SetStatus(state.StatusText);
        _activeView.IconSize = state.IconSize;
        _activeView.CurrentPath = state.ActivePath;
    }

    // ── Interaction event wiring ───────────────────────────────────

    private void WireUpInteractionEvents()
    {
        _interaction.NavigateRequested = path =>
        {
            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.NavigateTo(path));
        };

        _interaction.ContextMenuRequested = (screenPos, selectedPaths) =>
        {
            // Find the module that handles the first selected path and show its context menu.
            if (selectedPaths.Count > 0)
            {
                var handlerModule = App.Modules.FindByPath(selectedPaths[0]);
                handlerModule?.ContextMenuProvider?.Show(this, screenPos, selectedPaths);
            }
        };

        _interaction.ErrorDisplayRequested = message =>
        {
            MessageBox.Show(message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        _interaction.ConflictResolutionRequested = () =>
        {
            var choice = MessageBox.Show(
                "This location already contains items with the same name." +
                "\n\nReplace the existing items?\n\nYes = replace, No = keep existing items and copy the rest, Cancel = do nothing.",
                "Confirm Replace",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            return choice switch
            {
                MessageBoxResult.Yes => IFileOperations.ConflictPolicy.Overwrite,
                MessageBoxResult.No => IFileOperations.ConflictPolicy.Skip,
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

        _activeView.OpenInNewTabRequested += path => _tabs.AddTab(path);
    }

    // ── Sidebar and Navigation toolbar ─────────────────────────────

    private void WireUpSidebarAndNavEvents()
    {
        SidebarPanel.NavigateRequested += path =>
        {
            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.NavigateTo(path));
        };

        SidebarPanel.OpenInNewTabRequested += path => _tabs.AddTab(path);

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

        NavToolbar.NavigateToRequested += path =>
        {
            string target = path.Trim();
            if (string.IsNullOrEmpty(target)) return;

            // Let the module resolve display names to internal paths
            target = _module.ResolveDisplayName(target) ?? target;

            string previousPath = _tabs.ActiveTab?.State.ActivePath ?? "";

            _tabs.CommitActiveRename(_activeView);
            _tabs.DispatchActive(new TabAction.NavigateTo(target));

            // If navigation was rejected (invalid path), revert address bar and show alert
            if (_tabs.ActiveTab != null && _tabs.ActiveTab.State.ActivePath != target)
            {
                NavToolbar.SetPath(_module.FileProvider.GetDisplayPath(previousPath));
                AlertOverlay.ShowAlert(
                    $"Could not find \u201c{path}\u201d.",
                    "Please check the spelling and try again.");
            }
        };
    }

    // ── Zoom ───────────────────────────────────────────────────────

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

    // ── Command handlers ───────────────────────────────────────────

    private void Exit_Executed(object sender, ExecutedRoutedEventArgs e) =>
        Close();

    private void Rename_Executed(object sender, ExecutedRoutedEventArgs e)
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
        if (_tabs.ActiveTab == null) return;

        _tabs.CommitActiveRename(_activeView);
        foreach (var item in _tabs.ActiveTab.Folders.Where(i => i.IsSelected))
            _interaction.DeleteToTrash(item);
    }

    private void SelectAll_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.ActiveTab == null) return;

        foreach (var item in _tabs.ActiveTab.Folders)
            item.IsSelected = true;
    }

    private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.ActiveTab != null)
            _interaction.HandleCopy(_tabs.ActiveTab.Folders);
    }

    private void Cut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.ActiveTab != null)
            _interaction.HandleCut(_tabs.ActiveTab.Folders);
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

    // ── Tab strip UI ───────────────────────────────────────────────

    private void AddTabButton_Click(object sender, RoutedEventArgs e) =>
        _tabs.AddTab();

    // ── WndProc: clamp maximized size to per-monitor work area ─────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var rc = monitorInfo.rcWork;
                mmi.ptMaxPosition.x = rc.Left;
                mmi.ptMaxPosition.y = rc.Top;
                mmi.ptMaxSize.x = rc.Right - rc.Left;
                mmi.ptMaxSize.y = rc.Bottom - rc.Top;
                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    // ── Win32 interop for per-monitor work area ───────────────────

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ── Helpers ────────────────────────────────────────────────────

}