using System.Runtime.InteropServices;

namespace DotNetFM;

public sealed partial class ClickTracker
{
    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    private FolderItem? _lastClickedItem;
    private int _lastClickTick;

    public enum ClickAction
    {
        Select,
        BeginRename,
        Open,
    }

    public ClickAction RecordClick(FolderItem clickedItem)
    {
        int currentTick = Environment.TickCount;
        int delta = currentTick - _lastClickTick;
        uint threshold = GetDoubleClickTime();

        if (_lastClickedItem == clickedItem && delta > 0 && delta <= threshold)
        {
            _lastClickedItem = null;
            return ClickAction.Open;
        }

        var previousClicked = _lastClickedItem;
        _lastClickedItem = clickedItem;
        _lastClickTick = currentTick;

        if (clickedItem.IsSelected && previousClicked == clickedItem)
        {
            return ClickAction.BeginRename;
        }

        return ClickAction.Select;
    }

    public void Reset()
    {
        _lastClickedItem = null;
    }
}
