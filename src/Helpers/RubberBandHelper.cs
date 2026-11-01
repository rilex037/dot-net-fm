using System.Windows;

namespace DotNetFM;

public static class RubberBandHelper
{
    public static Rect ComputeSelectionRect(Point start, Point current)
    {
        double x = Math.Min(current.X, start.X);
        double y = Math.Min(current.Y, start.Y);
        double w = Math.Abs(current.X - start.X);
        double h = Math.Abs(current.Y - start.Y);
        return new Rect(x, y, w, h);
    }

    public static bool IsBeyondThreshold(Point start, Point current, double threshold = 3)
    {
        return Math.Abs(current.X - start.X) > threshold ||
               Math.Abs(current.Y - start.Y) > threshold;
    }

    public static void ApplySelection(
        IEnumerable<FolderItem> items,
        Rect selectionRect,
        Func<FolderItem, Rect?> getItemBounds,
        HashSet<FolderItem>? additiveSnapshot = null)
    {
        foreach (var item in items)
        {
            var itemRect = getItemBounds(item);
            bool intersects = itemRect.HasValue && selectionRect.IntersectsWith(itemRect.Value);

            if (additiveSnapshot != null)
            {
                if (intersects)
                    item.IsSelected = true;
                else if (!additiveSnapshot.Contains(item))
                    item.IsSelected = false;
            }
            else
            {
                item.IsSelected = intersects;
            }
        }
    }
}
