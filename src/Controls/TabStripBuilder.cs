using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SharpVectors.Converters;

namespace DotNetFM;

public sealed class TabStripBuilder
{
    private readonly IModule _module;
    private readonly TabManager _tabs;
    private readonly Dictionary<Guid, Action<TabStateRecord>> _titleHandlers = new();

    public TabStripBuilder(IModule module, TabManager tabs)
    {
        _module = module;
        _tabs = tabs;
    }

    public void Rebuild(Panel panel)
    {
        UnsubscribeAll();
        panel.Children.Clear();

        for (int i = 0; i < _tabs.Tabs.Count; i++)
        {
            var store = _tabs.Tabs[i];
            var tabId = store.State.TabId;
            bool isActive = _tabs.ActiveTab?.State.TabId == tabId;

            if (i > 0)
                panel.Children.Add(BuildSeparator());

            panel.Children.Add(BuildTab(store, tabId, isActive));
        }
    }

    private static UIElement BuildSeparator() => new Border
    {
        Width = 1,
        Background = (Brush)Application.Current.FindResource("SeparatorBrush"),
        VerticalAlignment = VerticalAlignment.Stretch,
        Margin = new Thickness(0, 7, 0, 7),
    };

    private UIElement BuildTab(TabStore store, Guid tabId, bool isActive)
    {
        var titleBlock = new TextBlock
        {
            Text = _module.FileProvider.GetDisplayTitle(store.State.ActivePath),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = (double)Application.Current.FindResource("FontTabTitleSize"),
            Foreground = (Brush)Application.Current.FindResource(isActive ? "TextPrimaryBrush" : "TextSecondaryBrush"),
            MinWidth = (double)Application.Current.FindResource("SizeTabTitleMinWidth"),
            MaxWidth = (double)Application.Current.FindResource("SizeTabTitleMaxWidth"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 8, 0),
        };

        double closeBtnSize = (double)Application.Current.FindResource("SizeTabCloseButtonSize");
        var iconPath = IconProvider.GetFullPath("window-close.svg");

        var closeButton = new Button
        {
            Width = closeBtnSize + 6,
            Height = closeBtnSize + 6,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (Brush)Application.Current.FindResource(isActive ? "TextPrimaryBrush" : "TextSecondaryBrush"),
            Cursor = Cursors.Hand,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = tabId,
            Style = (Style)Application.Current.FindResource("SubtleButton"),
            Content = new SvgViewbox
            {
                UriSource = new Uri(iconPath, UriKind.Absolute),
                Width = closeBtnSize * 0.5,
                Height = closeBtnSize * 0.5,
            },
        };

        closeButton.Click += (_, _) => _tabs.CloseTab(tabId);

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(titleBlock, 0);
        Grid.SetColumn(closeButton, 1);
        contentGrid.Children.Add(titleBlock);
        contentGrid.Children.Add(closeButton);

        var tabGrid = new Grid();
        tabGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tabGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
        Grid.SetRow(contentGrid, 0);
        tabGrid.Children.Add(contentGrid);

        var indicator = new Border
        {
            Height = 2,
            Background = (Brush)Application.Current.FindResource("AccentBrush"),
            Visibility = isActive ? Visibility.Visible : Visibility.Collapsed,
        };
        Grid.SetRow(indicator, 1);
        tabGrid.Children.Add(indicator);

        var border = new Border
        {
            Padding = (Thickness)Application.Current.FindResource("SpacingTabItemPadding"),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = isActive
                ? (Brush)Application.Current.FindResource("TabActiveBrush")
                : Brushes.Transparent,
            ToolTip = store.State.ActivePath,
            Child = tabGrid,
            Tag = tabId,
        };

        border.MouseLeftButtonDown += (_, e) =>
        {
            _tabs.SetActiveTab(tabId);
            e.Handled = true;
        };

        border.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                _tabs.CloseTab(tabId);
                e.Handled = true;
            }
        };

        border.MouseEnter += (_, _) =>
        {
            if (!isActive)
                border.Background = (Brush)Application.Current.FindResource("TabHoverBrush");
        };

        border.MouseLeave += (_, _) =>
        {
            if (!isActive)
                border.Background = Brushes.Transparent;
        };

        void TitleHandler(TabStateRecord s)
        {
            if (s.TabId == tabId)
            {
                titleBlock.Text = _module.FileProvider.GetDisplayTitle(s.ActivePath);
                border.ToolTip = s.ActivePath;
            }
        }
        _titleHandlers[tabId] = TitleHandler;
        store.StateChanged += TitleHandler;

        return border;
    }

    private void UnsubscribeAll()
    {
        foreach (var (tabId, handler) in _titleHandlers)
        {
            var store = _tabs.Tabs.FirstOrDefault(t => t.State.TabId == tabId);
            if (store != null)
                store.StateChanged -= handler;
        }
        _titleHandlers.Clear();
    }
}
