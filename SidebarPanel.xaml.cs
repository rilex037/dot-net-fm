using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dot_net_fm;

/// <summary>
/// Sidebar panel layout with My Computer, Network, and Bookmarks sections.
/// </summary>
public partial class SidebarPanel : UserControl
{
    public static readonly DependencyProperty MyComputerItemsProperty =
        DependencyProperty.Register(nameof(MyComputerItems), typeof(ObservableCollection<SidebarItem>), typeof(SidebarPanel));

    public static readonly DependencyProperty NetworkItemsProperty =
        DependencyProperty.Register(nameof(NetworkItems), typeof(ObservableCollection<SidebarItem>), typeof(SidebarPanel));

    public static readonly DependencyProperty BookmarkItemsProperty =
        DependencyProperty.Register(nameof(BookmarkItems), typeof(ObservableCollection<SidebarItem>), typeof(SidebarPanel));

    public ObservableCollection<SidebarItem>? MyComputerItems
    {
        get => (ObservableCollection<SidebarItem>?)GetValue(MyComputerItemsProperty);
        set => SetValue(MyComputerItemsProperty, value);
    }

    public ObservableCollection<SidebarItem>? NetworkItems
    {
        get => (ObservableCollection<SidebarItem>?)GetValue(NetworkItemsProperty);
        set => SetValue(NetworkItemsProperty, value);
    }

    public ObservableCollection<SidebarItem>? BookmarkItems
    {
        get => (ObservableCollection<SidebarItem>?)GetValue(BookmarkItemsProperty);
        set => SetValue(BookmarkItemsProperty, value);
    }

    /// <summary>
    /// Raised when the user clicks a sidebar item and requests navigation to its path.
    /// </summary>
    public event Action<string>? NavigateRequested;

    public SidebarPanel()
    {
        InitializeComponent();
    }

    private void SidebarItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SidebarItem sidebarItem)
        {
            string targetPath = sidebarItem.Path;
            if (!string.IsNullOrEmpty(targetPath) &&
                (targetPath == NavigationService.MyComputerPath || Directory.Exists(targetPath)))
            {
                NavigateRequested?.Invoke(targetPath);
            }
        }
    }
}
