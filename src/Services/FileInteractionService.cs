using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Specialized;
using Microsoft.VisualBasic.FileIO;

namespace dot_net_fm;

/// <summary>
/// Handles all file/folder item interactions:
/// click selection, slow double-click rename, fast double-click open,
/// rubber band selection, drag & drop, clipboard, and file operations.
/// Rename timer logic is delegated to <see cref="RenameManager"/>.
/// </summary>
public class FileInteractionService
{
    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    // Double-click tracking
    private FolderItem? _lastClickedItem;
    private int _lastClickTick;

    // Rubber band selection state
    private bool _isRubberBanding;
    private Point _rubberBandStart;

    // Rename state — delegated to RenameManager
    private readonly RenameManager _rename = new();

    // Drag & drop state
    private Point _dragStartPoint;
    private bool _dragThresholdArmed;

    // Clipboard state — system clipboard is the source of truth for paths;
    // this flag tracks whether the last operation was cut (move) or copy.
    private bool _clipboardIsCut;

    public Action<string>? NavigateRequested;
    public Action<string>? FileLaunchRequested;
    public Action<Point, List<string>>? ContextMenuRequested;

    // ──────────────────────────────── Selection ────────────────────────────

    /// <summary>
    /// Handles mouse down on the filename text.
    /// Single click selects, slow double-click starts rename, fast double-click opens.
    /// </summary>
    public void HandleNameMouseDown(FolderItem folderItem, ItemsControl itemsControl, MouseButtonEventArgs e)
    {
        if (folderItem.IsEditing) return;

        var folders = (ObservableCollection<FolderItem>)itemsControl.ItemsSource;
        CommitActiveRename(folders, itemsControl);

        int currentTick = Environment.TickCount;
        int delta = currentTick - _lastClickTick;
        uint doubleClickThreshold = GetDoubleClickTime();

        if (_lastClickedItem == folderItem && delta > 0 && delta <= doubleClickThreshold)
        {
            _rename.CancelPending();
            OpenItem(folderItem);
            e.Handled = true;
            _lastClickedItem = null;
            return;
        }

        _lastClickedItem = folderItem;
        _lastClickTick = currentTick;

        if (folderItem.IsSelected)
        {
            _rename.StartPending(folderItem, itemsControl);
        }
        else
        {
            _rename.CancelPending();
            ClearAllSelections(folders);
            folderItem.IsSelected = true;
        }

        ArmDrag(e.GetPosition(null));
        e.Handled = true;
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
        _rename.CancelPending();

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

        ArmDrag(e.GetPosition(null));
        e.Handled = true;
    }

    /// <summary>Clears all selections in the given folder collection.</summary>
    public void ClearAllSelections(ObservableCollection<FolderItem> folders)
    {
        foreach (var item in folders)
            item.IsSelected = false;
    }

    // ──────────────────────────────── Open ─────────────────────────────────

    /// <summary>
    /// Opens a folder (navigates) or file (launches process).
    /// </summary>
    public void OpenItem(FolderItem item)
    {
        if (item.IsFolder)
        {
            NavigateRequested?.Invoke(item.FullPath);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open '{item.Name}': {ex.Message}", "Open Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ──────────────────────────────── Rename ───────────────────────────────

    /// <summary>Commits an active rename on a given item.</summary>
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
            MessageBox.Show($"Rename failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Commits any active rename across the provided folder collection.
    /// Uses <see cref="RenameManager"/> to find the active item in O(1).
    /// </summary>
    public void CommitActiveRename(ObservableCollection<FolderItem> folders, ItemsControl? itemsControl)
    {
        _rename.CommitActive(itemsControl, CommitRename);
    }

    /// <summary>Starts rename mode on the given item.</summary>
    public void BeginRename(FolderItem item, ItemsControl? itemsControl)
    {
        _rename.BeginRename(item, itemsControl);
    }

    /// <summary>Handles rename TextBox key events (Enter to commit, Escape to cancel).</summary>
    public void HandleRenameKeyDown(KeyEventArgs e, TextBox textBox, FolderItem item)
    {
        if (e.Key == Key.Enter)
            CommitRename(textBox, item);
        else if (e.Key == Key.Escape)
            item.IsEditing = false;
    }

    // ──────────────────────────── Delete ───────────────────────────────────

    /// <summary>Sends the given item to the Recycle Bin (no prompt).</summary>
    public void DeleteToTrash(FolderItem item)
    {
        try
        {
            if (item.IsFolder)
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    item.FullPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
            }
            else
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    item.FullPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ──────────────────────────── Rubber band ──────────────────────────────

    /// <summary>Rubber band selection: mouse down on empty space.</summary>
    public void HandleRubberBandMouseDown(Point position, Canvas selectionCanvas, Border selectionBorder)
    {
        _rename.CancelPending();
        _dragThresholdArmed = false;

        _rubberBandStart = position;
        _isRubberBanding = false;

        selectionCanvas.CaptureMouse();
        selectionBorder.Visibility = Visibility.Collapsed;
    }

    /// <summary>Rubber band selection: mouse move.</summary>
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

    /// <summary>Rubber band selection: mouse up.</summary>
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
        => GetItemAtPosition(e, itemsControl) != null;

    public FolderItem? GetItemAtPosition(MouseButtonEventArgs e, ItemsControl itemsControl)
        => GetItemAtPoint(e.GetPosition(itemsControl), itemsControl);

    public FolderItem? GetItemAtPoint(Point position, ItemsControl itemsControl)
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

    // ──────────────────────────── Drag & Drop ──────────────────────────────

    public void ArmDrag(Point screenPosition)
    {
        _dragStartPoint = screenPosition;
        _dragThresholdArmed = true;
    }

    public bool UpdateDrag(DependencyObject dragSource, ObservableCollection<FolderItem> folders)
    {
        if (!_dragThresholdArmed || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            _dragThresholdArmed = false;
            return false;
        }

        var currentPos = Mouse.GetPosition(null);
        double dx = Math.Abs(currentPos.X - _dragStartPoint.X);
        double dy = Math.Abs(currentPos.Y - _dragStartPoint.Y);

        if (dx < 4 && dy < 4)
            return false;

        _dragThresholdArmed = false;
        _rename.CancelPending();

        var paths = GetSelectedPaths(folders);
        if (paths.Count == 0) return false;

        var data = new DataObject(DataFormats.FileDrop, paths);
        var result = DragDrop.DoDragDrop(dragSource, data, DragDropEffects.Move | DragDropEffects.Copy);
        return result == DragDropEffects.Move;
    }

    public void HandleDragOver(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Move | DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    public void HandleDrop(DragEventArgs e, string targetDirectory)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths == null || paths.Length == 0) return;

        TransferFiles(paths, targetDirectory, forceCopy: false);
        e.Handled = true;
    }

    // ──────────────────────────── Clipboard ────────────────────────────────

    public void HandleCopy(ObservableCollection<FolderItem> folders)
    {
        var paths = GetSelectedPaths(folders);
        if (paths.Count == 0) return;

        _clipboardIsCut = false;
        Clipboard.SetFileDropList(CreateStringCollection(paths));
    }

    public void HandleCut(ObservableCollection<FolderItem> folders)
    {
        var paths = GetSelectedPaths(folders);
        if (paths.Count == 0) return;

        _clipboardIsCut = true;
        Clipboard.SetFileDropList(CreateStringCollection(paths));
    }

    public void HandlePaste(string currentDirectory)
    {
        if (string.IsNullOrEmpty(currentDirectory)) return;
        if (!Clipboard.ContainsFileDropList()) return;

        var paths = Clipboard.GetFileDropList();
        if (paths.Count == 0) return;

        var arr = new string[paths.Count];
        paths.CopyTo(arr, 0);

        TransferFiles(arr, currentDirectory, forceCopy: !_clipboardIsCut);

        _clipboardIsCut = false;
    }

    // ──────────────────────────── File transfer ────────────────────────────

    /// <summary>
    /// Moves or copies files/directories to the target directory.
    /// Same-drive is moved by default; cross-drive is copied.
    /// Set <paramref name="forceCopy"/> to always copy (used by paste-after-copy).
    /// Duplicate names get a numeric suffix ("file - 1", "file - 2").
    /// </summary>
    private void TransferFiles(string[] sources, string targetDir, bool forceCopy)
    {
        foreach (var source in sources)
        {
            try
            {
                string name = Path.GetFileName(source);
                string dest = GetUniquePath(targetDir, name);
                bool sameDrive = string.Equals(
                    Path.GetPathRoot(source), Path.GetPathRoot(targetDir),
                    StringComparison.OrdinalIgnoreCase);

                if (File.Exists(source))
                {
                    if (!forceCopy && sameDrive)
                        File.Move(source, dest);
                    else
                        File.Copy(source, dest, overwrite: false);
                }
                else if (Directory.Exists(source))
                {
                    if (!forceCopy && sameDrive)
                        Directory.Move(source, dest);
                    else
                        FileSystem.CopyDirectory(source, dest);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transfer failed for '{Path.GetFileName(source)}': {ex.Message}",
                    "Transfer Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // ──────────────────────────── Helpers ──────────────────────────────────

    private static List<string> GetSelectedPaths(ObservableCollection<FolderItem> folders)
    {
        var paths = new List<string>();
        foreach (var item in folders)
            if (item.IsSelected)
                paths.Add(item.FullPath);
        return paths;
    }

    private static StringCollection CreateStringCollection(List<string> paths)
    {
        var collection = new StringCollection();
        foreach (var p in paths) collection.Add(p);
        return collection;
    }

    private static string GetUniquePath(string dir, string name)
    {
        string dest = Path.Combine(dir, name);
        if (!File.Exists(dest) && !Directory.Exists(dest))
            return dest;

        string ext = Path.GetExtension(name);
        string baseName = Path.GetFileNameWithoutExtension(name);
        int counter = 1;
        while (true)
        {
            string candidate = Path.Combine(dir, $"{baseName} - {counter}{ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
            counter++;
        }
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