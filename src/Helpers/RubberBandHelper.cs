using System.Windows;

namespace DotNetFM;

/// <summary>
/// Pure math for rubber-band selection rectangles.
/// No WPF dependency beyond System.Windows.Point/Size/Rect.
/// </summary>
public static class RubberBandHelper
{
    /// <summary>
    /// Computes the selection rectangle from start and current drag positions.
    /// </summary>
    public static Rect ComputeSelectionRect(Point start, Point current)
    {
        double x = Math.Min(current.X, start.X);
        double y = Math.Min(current.Y, start.Y);
        double w = Math.Abs(current.X - start.X);
        double h = Math.Abs(current.Y - start.Y);
        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// Determines if the drag distance exceeds the minimum threshold (3px) to begin rubber-banding.
    /// </summary>
    public static bool IsBeyondThreshold(Point start, Point current, double threshold = 3)
    {
        return Math.Abs(current.X - start.X) > threshold ||
               Math.Abs(current.Y - start.Y) > threshold;
    }

    /// <summary>
    /// Updates the IsSelected flag on items that intersect with the selection rectangle.
    /// The <paramref name="getItemBounds"/> callback should return the item's bounding rect
    /// relative to the same coordinate space as <paramref name="selectionRect"/>.
    /// When <paramref name="additiveSnapshot"/> is provided (Ctrl+drag), items that were
    /// selected before the rubber band started keep their selection even outside the band.
    /// </summary>
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
                // Additive: intersecting items get selected,
                // pre-existing selections outside the band are preserved.
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
