using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace dot_net_fm;

/// <summary>
/// Main window: thin layout shell that delegates all state to
/// <see cref="TabManager"/>. UI updates flow from a single
/// <see cref="TabStore.StateChanged"/> event per tab.
/// </summary>
public partial class MainWindow : Window
{
    private const int  WM_GETMINMAXINFO        = 0x0024;
    private const uint MONITOR_DEFAULTTONEAREST = 2u;

    private readonly TabManager             _tabs;
    private readonly FileInteractionService _interaction;
    private readonly DragDropService        _dragDrop = new();

    private readonly ObservableCollection<SidebarItem.Item> _myComputerItems = new();
    private readonly ObservableCollection<SidebarItem.Item> _networkItems    = new();
    private readonly ObservableCollection<SidebarItem.Item> _bookmarkItems   = new();

    private readonly string _userProfilePath;
    private TabStore? _subscribedTab;
    private readonly Dictionary<Guid, Action<TabStateRecord>> _tabTitleHandlers = new();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ((HwndSource)PresentationSource.FromVisual(this)).AddHook(WndProc);
    }

    public MainWindow(string initialPath = "")
    {
        InitializeComponent();

        // Resolve initial path: extract raw path from module URI (e.g. "windows://C:\Users" → "C:\Users")
        var moduleUri = ModuleUri.Parse(initialPath);
        string rawPath = moduleUri.Path;

        // If no path provided or path doesn't exist, fall back to a sensible default
        if (string.IsNullOrWhiteSpace(rawPath) || !System.IO.Directory.Exists(rawPath))
        {
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfilePath) && System.IO.Directory.Exists(userProfilePath))
                rawPath = userProfilePath;
            else
                rawPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        // Find the module that handles this path
        var module = App.Modules.FindByPath(rawPath);
        _userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _interaction = new FileInteractionService();
        _tabs        = new TabManager(rawPath, module);

        SidebarPanel.MyComputerItems = _myComputerItems;
        SidebarPanel.NetworkItems    = _networkItems;
        SidebarPanel.BookmarkItems   = _bookmarkItems;

        InitializeSidebar(rawPath);
        WireUpSidebarAndNavEvents();
        WireUpZoomEvents();
        WireUpTabManagerEvents();
        WireUpInteractionEvents();

        StatusBar.ZoomSlider.Value = 3;

        _tabs.AddTab();
    }

    // ── Sidebar initialization ─────────────────────────────────────

    private void InitializeSidebar(string initialPath)
    {
        SidebarItem config = SidebarService.Load();
        SidebarIconMapper.Initialize(config.SidebarIcons);

        // Use module-contributed sidebar sections instead of static config sections
        var module = App.Modules.FindByPath(initialPath);
        var sections = module?.GetSidebarSections()
            .OrderBy(s => s.Order)
            .ToList();

        if (sections != null)
        {
            foreach (var section in sections)
            {
                var target = section.Title.Equals("Network", StringComparison.OrdinalIgnoreCase)
                    ? _networkItems
                    : _myComputerItems;

                foreach (var entry in section.Entries)
                {
                    string resolvedPath = entry.Path;
                    string displayName  = entry.Name;

                    // "Home" entry shows the user's name when it points to the user profile directory
                    if (entry.Icon.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                        resolvedPath.Equals(_userProfilePath, StringComparison.OrdinalIgnoreCase))
                        displayName = Environment.UserName;

                    target.Add(new SidebarItem.Item
                    {
                        Name     = displayName,
                        IconPath = SidebarIconMapper.GetIconPath(entry.Icon),
                        Path     = resolvedPath,
                    });
                }
            }
        }

        // Bookmarks still come from the config file
        foreach (var bm in config.Bookmarks)
        {
            _bookmarkItems.Add(new SidebarItem.Item
            {
                Name     = bm.Name,
                IconPath = SidebarIconMapper.GetIconPath(bm.IconPath),
                Path     = SidebarService.ResolvePath(bm.Path),
            });
        }
    }

    // ── Tab Manager wiring ─────────────────────────────────────────

    private void WireUpTabManagerEvents()
    {
        _tabs.ActiveTabChanged += OnActiveTabChanged;
        _tabs.AllTabsClosed    += () => _tabs.AddTab();
        _tabs.TabClosed += _ => RefreshTabStrip();
    }

    private void OnActiveTabChanged(TabStore? newTab)
    {
        if (newTab == null) return;

        if (_subscribedTab != null)
            _subscribedTab.StateChanged -= OnActiveTabStateChanged;

        _subscribedTab = newTab;
        newTab.StateChanged += OnActiveTabStateChanged;

        FileGrid.Folders            = newTab.Folders;
        FileGrid.InteractionService = _interaction;
        FileGrid.DragDropService    = _dragDrop;
        FileGrid.CurrentPath        = newTab.State.ActivePath;
        FileGrid.IconSize           = newTab.State.IconSize;

        StatusBar.SetZoomForIconSize(newTab.State.IconSize);
        ApplyStateToUI(newTab.State);
        RefreshTabStrip();
    }

    private void OnActiveTabStateChanged(TabStateRecord state)
    {
        if (_tabs.ActiveTab?.State.TabId != state.TabId) return;
        ApplyStateToUI(state);
    }

    private void ApplyStateToUI(TabStateRecord state)
    {
        Title = state.Title;
        TitleBar.SetTitle(state.Title);
        NavToolbar.SetPath(state.DisplayPath);
        NavToolbar.UpdateNavStates(state.CanGoBack, state.CanGoForward, state.CanGoUp);
        StatusBar.SetStatus(state.StatusText);
        FileGrid.IconSize    = state.IconSize;
        FileGrid.CurrentPath = state.ActivePath;
    }

    // ── Interaction event wiring ───────────────────────────────────

    private void WireUpInteractionEvents()
    {
        _interaction.NavigateRequested = path =>
        {
            _tabs.CommitActiveRename(FileGrid);
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

        _interaction.RenameManager.RenameReady += item =>
        {
            _interaction.RenameManager.BeginRename(item);
            FileGrid.FocusRenameTextBox(item);
        };

        _dragDrop.TransferRequested = (paths, targetDir, forceCopy) =>
        {
            _interaction.FileOperations.TransferFiles(paths, targetDir, forceCopy);
        };
    }

    // ── Sidebar and Navigation toolbar ─────────────────────────────

    private void WireUpSidebarAndNavEvents()
    {
        SidebarPanel.NavigateRequested += path =>
        {
            _tabs.CommitActiveRename(FileGrid);
            _tabs.DispatchActive(new TabAction.NavigateTo(path));
        };

        NavToolbar.BackRequested += () =>
        {
            _tabs.CommitActiveRename(FileGrid);
            _tabs.DispatchActive(new TabAction.GoBack());
        };

        NavToolbar.ForwardRequested += () =>
        {
            _tabs.CommitActiveRename(FileGrid);
            _tabs.DispatchActive(new TabAction.GoForward());
        };

        NavToolbar.UpRequested += () =>
        {
            _tabs.CommitActiveRename(FileGrid);
            _tabs.DispatchActive(new TabAction.GoUp());
        };

        NavToolbar.NavigateToRequested += path =>
        {
            string target = path.Trim();
            if (string.IsNullOrEmpty(target)) return;

            if (target.Equals("my computer", StringComparison.OrdinalIgnoreCase))
                target = NavigationService.MyComputerPath;

            _tabs.CommitActiveRename(FileGrid);
            _tabs.DispatchActive(new TabAction.NavigateTo(target));
        };
    }

    // ── Zoom ───────────────────────────────────────────────────────

    private void WireUpZoomEvents()
    {
        FileGrid.MouseWheelPreview += e =>
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
        var selected = FindFirstSelected();
        if (selected != null)
        {
            _tabs.CommitActiveRename(FileGrid);
            _interaction.BeginRename(selected);
            FileGrid.FocusRenameTextBox(selected);
        }
    }

    private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = FindFirstSelected();
        if (selected != null)
        {
            _tabs.CommitActiveRename(FileGrid);
            _interaction.DeleteToTrash(selected);
        }
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
        _tabs.CommitActiveRename(FileGrid);
        _tabs.DispatchActive(new TabAction.BeginRefresh());
    }

    private void ZoomIn_Executed(object sender, ExecutedRoutedEventArgs e) =>
        StatusBar.ZoomSlider.Value = Math.Min(StatusBar.ZoomSlider.Maximum, StatusBar.ZoomSlider.Value + 1);

    private void ZoomOut_Executed(object sender, ExecutedRoutedEventArgs e) =>
        StatusBar.ZoomSlider.Value = Math.Max(StatusBar.ZoomSlider.Minimum, StatusBar.ZoomSlider.Value - 1);

    // ── Tab management commands ────────────────────────────────────

    private void NewTab_Executed(object sender, ExecutedRoutedEventArgs e) =>
        _tabs.AddTab();

    private void CloseTab_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.ActiveTab != null)
            _tabs.CloseTab(_tabs.ActiveTab.State.TabId);
    }

    private void NextTab_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.Tabs.Count < 2) return;
        int currentIdx = _tabs.ActiveTab != null ? _tabs.Tabs.IndexOf(_tabs.ActiveTab) : -1;
        _tabs.SetActiveTab((currentIdx + 1) % _tabs.Tabs.Count);
    }

    private void PrevTab_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.Tabs.Count < 2) return;
        int currentIdx = _tabs.ActiveTab != null ? _tabs.Tabs.IndexOf(_tabs.ActiveTab) : -1;
        _tabs.SetActiveTab((currentIdx - 1 + _tabs.Tabs.Count) % _tabs.Tabs.Count);
    }

    // ── Tab strip UI ───────────────────────────────────────────────

    private void AddTabButton_Click(object sender, RoutedEventArgs e) =>
        _tabs.AddTab();

    private void RefreshTabStrip()
    {
        foreach (var (tabId, handler) in _tabTitleHandlers)
        {
            var store = _tabs.Tabs.FirstOrDefault(t => t.State.TabId == tabId);
            if (store != null)
                store.StateChanged -= handler;
        }
        _tabTitleHandlers.Clear();
        TabStripPanel.Children.Clear();

        foreach (var store in _tabs.Tabs)
        {
            var  tabId    = store.State.TabId;
            bool isActive = _tabs.ActiveTab?.State.TabId == tabId;

            var titleBlock = new TextBlock
            {
                Text              = store.State.Title,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize          = (double)FindResource("FontTabTitleSize"),
                Foreground        = (Brush)FindResource("TextPrimaryBrush"),
                MinWidth          = 40,
                MaxWidth          = 150,
                TextTrimming      = TextTrimming.CharacterEllipsis,
                Margin            = new Thickness(0, 0, 4, 0),
            };

            var closeButton = new Button
            {
                Content           = "×",
                Width             = 16,
                Height            = 16,
                FontSize          = (double)FindResource("FontTabCloseSize"),
                Background        = Brushes.Transparent,
                BorderThickness   = new Thickness(0),
                Foreground        = (Brush)FindResource("TextSecondaryBrush"),
                Cursor            = Cursors.Hand,
                Focusable         = false,
                VerticalAlignment = VerticalAlignment.Center,
                Tag               = tabId,
            };
            closeButton.Click += (_, _) => _tabs.CloseTab(tabId);

            var stack = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            stack.Children.Add(titleBlock);
            stack.Children.Add(closeButton);

            var border = new Border
            {
                Padding      = new Thickness(8, 0, 4, 0),
                Height       = 24,
                Cursor       = Cursors.Hand,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Margin       = new Thickness(1, 0, 0, 0),
                Background   = isActive
                    ? (Brush)FindResource("SidebarBrush")
                    : Brushes.Transparent,
                Child        = stack,
                Tag          = tabId,
            };
            border.MouseLeftButtonDown += (_, _) => _tabs.SetActiveTab(tabId);

            void TitleHandler(TabStateRecord s)
            {
                if (s.TabId == tabId) titleBlock.Text = s.Title;
            }
            _tabTitleHandlers[tabId] = TitleHandler;
            store.StateChanged += TitleHandler;

            TabStripPanel.Children.Add(border);
        }
    }

    // ── WndProc: clamp maximized size to per-monitor work area ─────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr hMonitor   = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var    monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var rc = monitorInfo.rcWork;
                mmi.ptMaxPosition.x = rc.Left;
                mmi.ptMaxPosition.y = rc.Top;
                mmi.ptMaxSize.x     = rc.Right  - rc.Left;
                mmi.ptMaxSize.y     = rc.Bottom - rc.Top;
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
        public int  cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ── Helpers ────────────────────────────────────────────────────

    private FolderItem? FindFirstSelected()
    {
        if (_tabs.ActiveTab == null) return null;
        foreach (var item in _tabs.ActiveTab.Folders)
            if (item.IsSelected) return item;
        return null;
    }
}