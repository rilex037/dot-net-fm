using System;
using System.Windows.Threading;

namespace dot_net_fm;

/// <summary>
/// Encapsulates the slow-double-click-to-rename interaction:
/// debounce timer, active editing item tracking, and rename state.
/// No visual tree dependencies — the caller handles TextBox focus/selection.
/// </summary>
public sealed class RenameManager
{
    private readonly DispatcherTimer _timer;
    private FolderItem? _pendingItem;

    /// <summary>The item currently in rename mode, or null.</summary>
    public FolderItem? ActiveEditingItem { get; private set; }

    /// <summary>Fired when the debounce timer expires and rename should begin on the pending item.</summary>
    public event Action<FolderItem>? RenameReady;

    public RenameManager()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Starts the debounce timer for the given item. If the timer expires
    /// while the item is still selected, <see cref="RenameReady"/> fires.
    /// </summary>
    public void StartPending(FolderItem item)
    {
        CancelPending();
        _pendingItem = item;
        _timer.Start();
    }

    /// <summary>Stops the timer without starting rename.</summary>
    public void CancelPending()
    {
        _timer.Stop();
        _pendingItem = null;
    }

    /// <summary>Enters rename mode on the given item.</summary>
    public void BeginRename(FolderItem item)
    {
        ActiveEditingItem = item;
        item.EditName = item.Name;
        item.IsEditing = true;
    }

    /// <summary>Commits the rename using the provided callback if an item is actively editing.</summary>
    public bool CommitActive(Action<FolderItem>? onCommitted)
    {
        if (ActiveEditingItem == null || !ActiveEditingItem.IsEditing)
        {
            ClearActive();
            return false;
        }

        onCommitted?.Invoke(ActiveEditingItem);
        ClearActive();
        return true;
    }

    /// <summary>Clears the active editing state without committing.</summary>
    public void ClearActive()
    {
        if (ActiveEditingItem != null)
        {
            ActiveEditingItem.IsEditing = false;
            ActiveEditingItem = null;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();

        if (_pendingItem != null && _pendingItem.IsSelected)
        {
            RenameReady?.Invoke(_pendingItem);
        }

        _pendingItem = null;
    }
}
