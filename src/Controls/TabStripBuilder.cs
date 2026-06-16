using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DotNetFM;

/// <summary>
/// Builds tab strip UI elements from tab stores. Single responsibility:
/// translate tab state into visual tab items inside a panel.
/// </summary>
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

    /// <summary>
    /// Rebuilds the tab strip panel from current tab state.
    /// Unsubscribes previous title handlers, creates fresh UI, and wires new handlers.
    /// </summary>
    public void Rebuild(Panel panel)
    {
        UnsubscribeAll();
        panel.Children.Clear();

        foreach (var store in _tabs.Tabs)
        {
            var tabId = store.State.TabId;
            bool isActive = _tabs.ActiveTab?.State.TabId == tabId;

            var titleBlock = new TextBlock
            {
                Text = _module.FileProvider.GetDisplayTitle(store.State.ActivePath),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = (double)Application.Current.FindResource("FontTabTitleSize"),
                Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush"),
                MinWidth = (double)Application.Current.FindResource("SizeTabTitleMinWidth"),
                MaxWidth = (double)Application.Current.FindResource("SizeTabTitleMaxWidth"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 8, 0),
            };

            double closeBtnSize = (double)Application.Current.FindResource("SizeTabCloseButtonSize");

            var closeButton = new Button
            {
                Content = "×",
                Width = closeBtnSize,
                Height = closeBtnSize,
                FontSize = (double)Application.Current.FindResource("FontTabCloseSize"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)Application.Current.FindResource("TextSecondaryBrush"),
                Cursor = Cursors.Hand,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = tabId,
            };

            closeButton.Click += (_, _) => _tabs.CloseTab(tabId);

            var tabGrid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(titleBlock, 0);
            Grid.SetColumn(closeButton, 1);
            tabGrid.Children.Add(titleBlock);
            tabGrid.Children.Add(closeButton);

            var border = new Border
            {
                Padding = (Thickness)Application.Current.FindResource("SpacingTabItemPadding"),
                Height = (double)Application.Current.FindResource("SizeTabItemHeight"),
                Cursor = Cursors.Hand,
                CornerRadius = (CornerRadius)Application.Current.FindResource("CornerRadiusTabItem"),
                Margin = (Thickness)Application.Current.FindResource("SpacingTabItemMargin"),
                Background = isActive
                    ? (Brush)Application.Current.FindResource("BackgroundBrush")
                    : Brushes.Transparent,
                Child = tabGrid,
                Tag = tabId,
            };
            border.MouseLeftButtonDown += (_, _) => _tabs.SetActiveTab(tabId);

            void TitleHandler(TabStateRecord s)
            {
                if (s.TabId == tabId)
                    titleBlock.Text = _module.FileProvider.GetDisplayTitle(s.ActivePath);
            }
            _titleHandlers[tabId] = TitleHandler;
            store.StateChanged += TitleHandler;

            panel.Children.Add(border);
        }
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