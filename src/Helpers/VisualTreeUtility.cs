using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DotNetFM;

public static class VisualTreeUtility
{
    public static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var result = FindDescendant<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    public static FolderItem? GetFolderItemAtPoint(ItemsControl itemsControl, Point position)
    {
        var hit = VisualTreeHelper.HitTest(itemsControl, position);
        if (hit == null) return null;

        DependencyObject? obj = hit.VisualHit;
        while (obj != null && obj != itemsControl)
        {
            if (obj is ContentPresenter cp && cp.DataContext is FolderItem item)
                return item;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    public static SidebarItem.Item? GetSidebarItemAtPoint(ItemsControl itemsControl, Point position)
    {
        var hit = VisualTreeHelper.HitTest(itemsControl, position);
        if (hit == null) return null;

        DependencyObject? obj = hit.VisualHit;
        while (obj != null && obj != itemsControl)
        {
            if (obj is Border border && border.DataContext is SidebarItem.Item item)
                return item;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    public static T? FindDataContext<T>(DependencyObject element) where T : class
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    public static List<string> GetSelectedPaths(IEnumerable<FolderItem> folders)
    {
        var paths = new List<string>();
        foreach (var item in folders)
            if (item.IsSelected)
                paths.Add(item.FullPath);
        return paths;
    }
}
