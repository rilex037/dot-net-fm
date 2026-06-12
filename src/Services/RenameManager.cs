using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace dot_net_fm;

/// <summary>
/// Encapsulates the slow-double-click-to-rename interaction:
/// debounce timer, active editing item tracking, and rename state.
/// </summary>
public sealed class RenameManager
{
    private readonly DispatcherTimer _timer;
    private FolderItem? _pendingItem;
    private ItemsControl? _pendingItemsControl;

    /// <summary>The item currently in rename mode, or null.</summary>
    public FolderItem? ActiveEditingItem { get; private set; }

    public RenameManager()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Starts the debounce timer for the given item. If the timer expires
    /// while the item is still selected, rename begins.
    /// </summary>
    public void StartPending(FolderItem item, ItemsControl itemsControl)
    {
        CancelPending();
        _pendingItem = item;
        _pendingItemsControl = itemsControl;
        _timer.Start();
    }

    /// <summary>Stops the timer without starting rename.</summary>
    public void CancelPending()
    {
        _timer.Stop();
        _pendingItem = null;
        _pendingItemsControl = null;
    }

    /// <summary>Enters rename mode on the given item.</summary>
    public void BeginRename(FolderItem item, ItemsControl? itemsControl)
    {
        ActiveEditingItem = item;
        item.EditName = item.Name;
        item.IsEditing = true;

        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var container = itemsControl?.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            var textBox = container != null ? FindDescendant<TextBox>(container) : null;
            if (textBox != null)
            {
                textBox.Focus();
                if (!item.IsFolder)
                {
                    int nameLen = System.IO.Path.GetFileNameWithoutExtension(item.Name).Length;
                    textBox.Select(0, nameLen);
                }
                else
                {
                    textBox.SelectAll();
                }
            }
        });
    }

    /// <summary>Commits the rename on the active item using the provided commit callback.</summary>
    public bool CommitActive(ItemsControl? itemsControl, Action<TextBox, FolderItem> commitCallback)
    {
        if (ActiveEditingItem == null || !ActiveEditingItem.IsEditing)
        {
            ClearActive();
            return false;
        }

        var container = itemsControl?.ItemContainerGenerator.ContainerFromItem(ActiveEditingItem) as ContentPresenter;
        var textBox = container != null ? FindDescendant<TextBox>(container) : null;

        if (textBox != null)
            commitCallback(textBox, ActiveEditingItem);
        else
            ActiveEditingItem.IsEditing = false;

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

        if (_pendingItem != null && _pendingItem.IsSelected && _pendingItemsControl != null)
        {
            BeginRename(_pendingItem, _pendingItemsControl);
        }

        _pendingItem = null;
        _pendingItemsControl = null;
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            var result = FindDescendant<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }
}