using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace dot_net_fm;

/// <summary>
/// Shared helpers for visual tree traversal.
/// Eliminates duplicate FindDescendant implementations.
/// </summary>
public static class VisualTreeUtility
{
    /// <summary>
    /// Finds the first descendant of type <typeparamref name="T"/> in the visual tree.
    /// </summary>
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

    /// <summary>
    /// Finds the <see cref="FolderItem"/> data context at the given position within an ItemsControl,
    /// walking up the visual tree from the hit-test result.
    /// </summary>
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
}