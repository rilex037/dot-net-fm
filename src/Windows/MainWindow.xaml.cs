using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace dot_net_fm;

/// <summary>
/// Main window: thin layout shell that delegates all state to
/// <see cref="TabManager"/>. UI updates flow from a single
/// <see cref="TabStore.StateChanged"/> event per tab.
/// </summary>
public partial class MainWindow : Window
{
    private readonly TabManager _tabs;
    private readonly FileInteractionService _interaction = new();
    private readonly DragDropService _dragDrop = new();

    private readonly ObservableCollection<SidebarItem.Item> _myComputerItems = new();
    private readonly ObservableCollection<SidebarItem.Item> _networkItems    = new();
    private readonly ObservableCollection<SidebarItem.Item> _bookmarkItems   = new();

    public MainWindow()
    {
        InitializeComponent();

        string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _tabs = new TabManager(userProfilePath);

        // Assign sidebar collections before populating them
        SidebarPanel.MyComputerItems = _myComputerItems;
        SidebarPanel.NetworkItems    = _networkItems;
        SidebarPanel.BookmarkItems   = _bookmarkItems;

        InitializeSidebar();
        WireUpSidebarAndNavEvents();
        WireUpZoomEvents();
        WireUpTabManagerEvents();
        WireUpInteractionEvents();

        StatusBar.ZoomSlider.Value = 3;

        // Create initial tab
        _tabs.AddTab();
    }

    // ── Sidebar initialization ─────────────────────────────────────

    private void InitializeSidebar()
    {
        string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        SidebarItem config = SidebarService.Load();
        SidebarIconMapper.Initialize(config.SidebarIcons);

        foreach (var section in config.Sections)
        {
            var target = section.Name.Equals("Network", StringComparison.OrdinalIgnoreCase)
                ? _networkItems
                : _myComputerItems;

            foreach (var entry in section.Items)
            {
                string resolvedPath = SidebarService.ResolvePath(entry.Path);
                string displayName  = entry.Name;
                if (entry.Name.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
                    resolvedPath.Equals(userProfilePath, StringComparison.OrdinalIgnoreCase))
                    displayName = Environment.UserName;

                target.Add(new SidebarItem.Item
                {
                    Name     = displayName,
                    IconPath = SidebarIconMapper.GetIconPath(entry.IconPath),
                    Path     = resolvedPath,
                });
            }
        }

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
        _tabs.AllTabsClosed     += () => _tabs.AddTab();
    }

    private void OnActiveTabChanged(TabStore? newTab)
    {
        if (newTab == null) return;

        // Unsubscribe from previous tab
        // (not needed since we re-subscribe below)

        // Bind FileGrid to the new tab's data
        FileGrid.Folders = newTab.Folders;
        FileGrid.InteractionService = _interaction;
        FileGrid.DragDropService = _dragDrop;
        FileGrid.CurrentPath = newTab.State.ActivePath;
        FileGrid.IconSize = newTab.State.IconSize;

        // Sync zoom slider to the new tab's icon size
        StatusBar.SetZoomForIconSize(newTab.State.IconSize);

        // Update all UI from state
        ApplyStateToUI(newTab.State);

        // Subscribe to state changes on the new tab
        newTab.StateChanged += OnActiveTabStateChanged;

        // Refresh tab strip visuals
        RefreshTabStrip();
    }

    private void OnActiveTabStateChanged(TabStateRecord state)
    {
        // Only update UI if this is still the active tab
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
        FileGrid.IconSize = state.IconSize;
        FileGrid.CurrentPath = state.ActivePath;
    }

    // ── Interaction event wiring ───────────────────────────────────

    private void WireUpInteractionEvents()
    {
        _interaction.NavigateRequested = path =>
        {
            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
            _tabs.DispatchActive(new TabAction.NavigateTo(path));
        };

        _interaction.ContextMenuRequested = (screenPos, selectedPaths) =>
        {
            ShellContextMenuService.Show(this, screenPos, selectedPaths);
        };

        _dragDrop.TransferRequested = (paths, targetDir, forceCopy) =>
        {
            _interaction.TransferFiles(paths, targetDir, forceCopy);
        };
    }

    // ── Sidebar and Navigation toolbar ─────────────────────────────

    private void WireUpSidebarAndNavEvents()
    {
        SidebarPanel.NavigateRequested += path =>
        {
            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
            _tabs.DispatchActive(new TabAction.NavigateTo(path));
        };

        NavToolbar.BackRequested       += () =>
        {
            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
            _tabs.DispatchActive(new TabAction.GoBack());
        };

        NavToolbar.ForwardRequested    += () =>
        {
            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
            _tabs.DispatchActive(new TabAction.GoForward());
        };

        NavToolbar.UpRequested         += () =>
        {
            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
            _tabs.DispatchActive(new TabAction.GoUp());
        };

        NavToolbar.NavigateToRequested += path =>
        {
            string target = path.Trim();
            if (string.IsNullOrEmpty(target)) return;

            if (target.Equals("my computer", StringComparison.OrdinalIgnoreCase))
                target = NavigationService.MyComputerPath;

            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
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
            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
            _interaction.BeginRename(selected, FileGrid.FolderItemsControl);
        }
    }

    private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = FindFirstSelected();
        if (selected != null)
        {
            _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
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
        _tabs.CommitActiveRename(_interaction, FileGrid.FolderItemsControl);
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
        int currentIdx = _tabs.ActiveTab != null
            ? _tabs.Tabs.IndexOf(_tabs.ActiveTab)
            : -1;
        int nextIdx = (currentIdx + 1) % _tabs.Tabs.Count;
        _tabs.SetActiveTab(nextIdx);
    }

    private void PrevTab_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (_tabs.Tabs.Count < 2) return;
        int currentIdx = _tabs.ActiveTab != null
            ? _tabs.Tabs.IndexOf(_tabs.ActiveTab)
            : -1;
        int prevIdx = (currentIdx - 1 + _tabs.Tabs.Count) % _tabs.Tabs.Count;
        _tabs.SetActiveTab(prevIdx);
    }

    // ── Tab strip UI ───────────────────────────────────────────────

    private void AddTabButton_Click(object sender, RoutedEventArgs e) =>
        _tabs.AddTab();

    private void TabButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Guid tabId)
            _tabs.SetActiveTab(tabId);
    }

    private void TabCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Guid tabId)
            _tabs.CloseTab(tabId);
    }

    private void RefreshTabStrip()
    {
        TabStripPanel.Children.Clear();

        foreach (var store in _tabs.Tabs)
        {
            var tabId = store.State.TabId;
            bool isActive = _tabs.ActiveTab?.State.TabId == tabId;

            var titleBlock = new TextBlock
            {
                Text            = store.State.Title,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize        = 11,
                Foreground      = (Brush)FindResource("TextPrimaryBrush"),
                MinWidth        = 40,
                MaxWidth        = 150,
                TextTrimming    = TextTrimming.CharacterEllipsis,
                Margin          = new Thickness(0, 0, 4, 0)
            };

            var closeButton = new Button
            {
                Content         = "×",
                Width           = 16,
                Height          = 16,
                FontSize        = 10,
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground      = (Brush)FindResource("TextSecondaryBrush"),
                Cursor          = Cursors.Hand,
                Focusable       = false,
                VerticalAlignment = VerticalAlignment.Center,
                Tag             = tabId
            };
            closeButton.Click += (s, e) => _tabs.CloseTab(tabId);

            var stack = new StackPanel
            {
                Orientation      = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(titleBlock);
            stack.Children.Add(closeButton);

            var border = new Border
            {
                Padding    = new Thickness(8, 0, 4, 0),
                Height     = 24,
                Cursor     = Cursors.Hand,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Margin     = new Thickness(1, 0, 0, 0),
                Background = isActive
                    ? (Brush)FindResource("SidebarBrush")
                    : Brushes.Transparent,
                Child      = stack,
                Tag        = tabId
            };
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (s is FrameworkElement fe && fe.Tag is Guid id)
                    _tabs.SetActiveTab(id);
            };

            // Subscribe to title changes for this tab
            var capturedTitle = titleBlock;
            var capturedTabId = tabId;
            store.StateChanged += s =>
            {
                if (s.TabId == capturedTabId)
                    capturedTitle.Text = s.Title;
            };

            TabStripPanel.Children.Add(border);
        }
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