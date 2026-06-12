using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;

namespace FmDn;

/// <summary>
/// Handles all file/folder item interactions:
/// click selection, slow double-click rename, fast double-click open,
/// rubber band selection, and rename operations.
/// </summary>
public class FileInteractionService
{
    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    // Explicit native timing tracking to guarantee expiration logic works
    private FolderItem? _lastClickedItem;
    private int _lastClickTick;

    // Rubber band selection state
    private bool _isRubberBanding;
    private Point _rubberBandStart;

    // Debounce timer for rename intent
    private DispatcherTimer? _renameTimer;

    public Action<string>? NavigateRequested;
    public Action<string>? FileLaunchRequested;
    public Action<Point, List<string>>? ContextMenuRequested;

    private void CancelPendingRename()
    {
        if (_renameTimer != null)
        {
            _renameTimer.Stop();
            _renameTimer = null;
        }
    }

    /// <summary>
    /// Handles mouse down on the filename text.
    /// Uses absolute system double-click speed to enforce expiration.
    /// </summary>
    public void HandleNameMouseDown(FolderItem folderItem, ItemsControl itemsControl, MouseButtonEventArgs e)
    {
        if (folderItem.IsEditing) return;

        var folders = (ObservableCollection<FolderItem>)itemsControl.ItemsSource;
        CommitActiveRename(folders, itemsControl);

        int currentTick = Environment.TickCount;
        int delta = currentTick - _lastClickTick;
        uint doubleClickThreshold = GetDoubleClickTime();

        // Enforce math-based expiration check using native OS intervals
        if (_lastClickedItem == folderItem && delta > 0 && delta <= doubleClickThreshold)
        {
            CancelPendingRename();
            OpenItem(folderItem);
            e.Handled = true;
            _lastClickedItem = null; 
            return;
        }

        _lastClickedItem = folderItem;
        _lastClickTick = currentTick;

        if (folderItem.IsSelected)
        {
            // Already selected item clicked outside the double-click window: trigger rename
            CancelPendingRename();
            _renameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _renameTimer.Tick += (s, args) =>
            {
                CancelPendingRename();
                if (folderItem.IsSelected && _lastClickedItem == folderItem)
                {
                    BeginRename(folderItem, itemsControl);
                }
            };
            _renameTimer.Start();
        }
        else
        {
            CancelPendingRename();
            ClearAllSelections(folders);
            folderItem.IsSelected = true;
        }

        e.Handled = true; // Block bubbling so parent controls don't trigger default actions
    }

    /// <summary>
    /// Handles mouse down on the item icon.
    /// Single click selects, double click opens. Never triggers rename.
    /// </summary>
    public void HandleIconMouseDown(FolderItem folderItem, ItemsControl itemsControl, MouseButtonEventArgs e)
    {
        if (folderItem.IsEditing) return;

        var folders = (ObservableCollection<FolderItem>)itemsControl.ItemsSource;
        CommitActiveRename(folders, itemsControl);
        CancelPendingRename();

        int currentTick = Environment.TickCount;
        int delta = currentTick - _lastClickTick;
        uint doubleClickThreshold = GetDoubleClickTime();

        if (_lastClickedItem == folderItem && delta > 0 && delta <= doubleClickThreshold)
        {
            OpenItem(folderItem);
            e.Handled = true;
            _lastClickedItem = null;
            return;
        }

        _lastClickedItem = folderItem;
        _lastClickTick = currentTick;

        if (!folderItem.IsSelected)
        {
            ClearAllSelections(folders);
            folderItem.IsSelected = true;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Commits an active rename on a given item.
    /// </summary>
    public void CommitRename(TextBox textBox, FolderItem item)
    {
        if (!item.IsEditing) return;

        item.IsEditing = false;
        string newName = textBox.Text.Trim();

        if (string.IsNullOrEmpty(newName) || newName == item.Name)
            return;

        string? dir = Path.GetDirectoryName(item.FullPath);
        if (dir == null) return;

        string newPath = Path.Combine(dir, newName);

        try
        {
            if (item.IsFolder)
            {
                if (!Directory.Exists(item.FullPath) || Directory.Exists(newPath))
                    return;
                Directory.Move(item.FullPath, newPath);
            }
            else
            {
                if (!File.Exists(item.FullPath) || File.Exists(newPath))
                    return;
                File.Move(item.FullPath, newPath);
            }

            item.Name = newName;
            item.FullPath = newPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Rename failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Commits any active rename across the provided folder collection.
    /// </summary>
    public void CommitActiveRename(ObservableCollection<FolderItem> folders, ItemsControl? itemsControl)
    {
        foreach (var item in folders)
        {
            if (!item.IsEditing) continue;

            var container = itemsControl?.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            var textBox = container != null ? FindDescendant<TextBox>(container) : null;

            if (textBox != null)
                CommitRename(textBox, item);
            else
                item.IsEditing = false;

            return;
        }
    }

    /// <summary>
    /// Clears all selections in the given folder collection.
    /// </summary>
    public void ClearAllSelections(ObservableCollection<FolderItem> folders)
    {
        foreach (var item in folders)
        {
            item.IsSelected = false;
        }
    }

    /// <summary>
    /// Opens a folder (navigates) or file (launches process).
    /// </summary>
    public void OpenItem(FolderItem item)
    {
        if (item.IsFolder)
        {
            NavigateRequested?.Invoke(item.FullPath);
        }
        else
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FullPath) { UseShellExecute = true });
            }
            catch { }
        }
    }

    /// <summary>
    /// Starts rename mode on the given item.
    /// </summary>
    public void BeginRename(FolderItem item, ItemsControl? itemsControl)
    {
        Dispatcher dispatcher = Application.Current.Dispatcher;
        item.EditName = item.Name;
        item.IsEditing = true;

        dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var container = itemsControl?.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            var textBox = container != null ? FindDescendant<TextBox>(container) : null;
            if (textBox != null)
            {
                textBox.Focus();
                if (!item.IsFolder)
                {
                    int nameLen = Path.GetFileNameWithoutExtension(item.Name).Length;
                    textBox.Select(0, nameLen);
                }
                else
                {
                    textBox.SelectAll();
                }
            }
        });
    }

    /// <summary>
    /// Handles rename TextBox key events (Enter to commit, Escape to cancel).
    /// </summary>
    public void HandleRenameKeyDown(KeyEventArgs e, TextBox textBox, FolderItem item)
    {
        if (e.Key == Key.Enter)
        {
            CommitRename(textBox, item);
        }
        else if (e.Key == Key.Escape)
        {
            item.IsEditing = false;
        }
    }

    /// <summary>
    /// Sends the given item to the Recycle Bin (no prompt).
    /// </summary>
    public void DeleteToTrash(FolderItem item)
    {
        try
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                item.FullPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin);
        }
        catch
        {
            // If it's a directory, try that
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    item.FullPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
            }
            catch { }
        }
    }

    /// <summary>
    /// Handles rubber band selection: mouse down on empty space.
    /// </summary>
    public void HandleRubberBandMouseDown(Point position, Canvas selectionCanvas, Border selectionBorder)
    {
        CancelPendingRename();

        _rubberBandStart = position;
        _isRubberBanding = false;

        selectionCanvas.CaptureMouse();
        selectionBorder.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Handles rubber band selection: mouse move.
    /// </summary>
    public void HandleRubberBandMouseMove(Point position, Canvas selectionCanvas, Border selectionBorder)
    {
        if (Mouse.Captured != selectionCanvas)
            return;

        double dx = Math.Abs(position.X - _rubberBandStart.X);
        double dy = Math.Abs(position.Y - _rubberBandStart.Y);

        if (dx > 3 || dy > 3)
        {
            _isRubberBanding = true;
            selectionBorder.Visibility = Visibility.Visible;
            UpdateSelectionRect(position, selectionCanvas, selectionBorder);
        }
    }

    /// <summary>
    /// Handles rubber band selection: mouse up.
    /// </summary>
    public void HandleRubberBandMouseUp(Canvas selectionCanvas, Border selectionBorder)
    {
        if (Mouse.Captured != selectionCanvas)
            return;

        selectionCanvas.ReleaseMouseCapture();
        selectionBorder.Visibility = Visibility.Collapsed;

        _isRubberBanding = false;
    }

    public bool IsRubberBanding => _isRubberBanding;

    public bool IsClickOnItem(MouseButtonEventArgs e, ItemsControl itemsControl)
    {
        return GetItemAtPosition(e, itemsControl) != null;
    }

    public FolderItem? GetItemAtPosition(MouseButtonEventArgs e, ItemsControl itemsControl)
    {
        var pos = e.GetPosition(itemsControl);
        var hit = VisualTreeHelper.HitTest(itemsControl, pos);
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

    private void UpdateSelectionRect(Point current, Canvas selectionCanvas, Border selectionBorder)
    {
        double x = Math.Min(current.X, _rubberBandStart.X);
        double y = Math.Min(current.Y, _rubberBandStart.Y);
        double w = Math.Abs(current.X - _rubberBandStart.X);
        double h = Math.Abs(current.Y - _rubberBandStart.Y);

        Canvas.SetLeft(selectionBorder, x);
        Canvas.SetTop(selectionBorder, y);
        selectionBorder.Width = w;
        selectionBorder.Height = h;
    }

    public void UpdateRubberBandSelection(
        ObservableCollection<FolderItem> folders,
        ItemsControl itemsControl,
        Canvas selectionCanvas,
        Border selectionBorder)
    {
        var rect = new Rect(
            Canvas.GetLeft(selectionBorder),
            Canvas.GetTop(selectionBorder),
            selectionBorder.Width,
            selectionBorder.Height);

        foreach (var item in folders)
        {
            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            if (container != null)
            {
                var topLeft = container.TranslatePoint(new Point(0, 0), selectionCanvas);
                var itemRect = new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));
                item.IsSelected = rect.IntersectsWith(itemRect);
            }
        }
    }

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
}
