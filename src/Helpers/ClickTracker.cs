using System;
using System.Runtime.InteropServices;

namespace dot_net_fm;

/// <summary>
/// Tracks mouse clicks to differentiate between single-click, slow double-click (rename),
/// and fast double-click (open). Uses Win32 GetDoubleClickTime for the threshold.
/// Pure state machine — no UI dependencies.
/// </summary>
public sealed class ClickTracker
{
    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private FolderItem? _lastClickedItem;
    private int _lastClickTick;

    /// <summary>
    /// Describes what action to take based on the current click context.
    /// </summary>
    public enum ClickAction
    {
        /// <summary>First click on a new item — select it.</summary>
        Select,
        /// <summary>Slow double-click on the same selected item — start rename.</summary>
        BeginRename,
        /// <summary>Fast double-click on the same item — open it.</summary>
        Open,
    }

    /// <summary>
    /// Records a click on the given item and returns the appropriate action.
    /// </summary>
    public ClickAction RecordClick(FolderItem clickedItem)
    {
        int currentTick = Environment.TickCount;
        int delta = currentTick - _lastClickTick;
        uint threshold = GetDoubleClickTime();

        if (_lastClickedItem == clickedItem && delta > 0 && delta <= threshold)
        {
            // Fast double-click — open
            _lastClickedItem = null;
            return ClickAction.Open;
        }

        _lastClickedItem = clickedItem;
        _lastClickTick = currentTick;

        if (clickedItem.IsSelected)
        {
            // Slow double-click (if clicked again after timer expires) — rename
            return ClickAction.BeginRename;
        }

        return ClickAction.Select;
    }

    /// <summary>Resets the click state without affecting the current selection.</summary>
    public void Reset()
    {
        _lastClickedItem = null;
    }
}