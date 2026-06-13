using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace dot_net_fm;

/// <summary>
/// Handles drag & drop initiation, drag-over feedback, and drop execution.
/// File transfer on drop is delegated via <see cref="TransferRequested"/>.
/// This separation allows cross-tab drops later (drop on tab strip).
/// </summary>
public class DragDropService
{
    private Point _dragStartPoint;
    private bool _dragThresholdArmed;

    /// <summary>
    /// Invoked when a drop should transfer files.
    /// Parameters: sourcePaths, targetDirectory, forceCopy.
    /// </summary>
    public Action<string[], string, bool>? TransferRequested;

    public void ResetDragThreshold()
    {
        _dragThresholdArmed = false;
    }

    public void ArmDrag(Point screenPosition)
    {
        _dragStartPoint = screenPosition;
        _dragThresholdArmed = true;
    }

    /// <summary>
    /// Checks drag threshold and initiates <see cref="DragDrop.DoDragDrop"/> if exceeded.
    /// Returns true if the drag resulted in a Move operation.
    /// </summary>
    public bool UpdateDrag(DependencyObject dragSource, IEnumerable<FolderItem> folders)
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

        var paths = GetSelectedPaths(folders);
        if (paths.Count == 0) return false;

        var data = new DataObject(DataFormats.FileDrop, paths.ToArray());
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

    /// <summary>
    /// Handles a drop: extracts file paths, guards against same-directory and
    /// self-drop, then invokes <see cref="TransferRequested"/>.
    /// </summary>
    public void HandleDrop(DragEventArgs e, string targetDirectory)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths == null || paths.Length == 0) return;

        // Prevent dropping items into their own directory (same-view drag)
        string? sourceDir = Path.GetDirectoryName(paths[0]);
        if (sourceDir != null &&
            string.Equals(Path.GetFullPath(sourceDir), Path.GetFullPath(targetDirectory), StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
            return;
        }

        // Prevent dropping a parent folder into itself (e.g. "FolderA" onto its subfolder)
        foreach (var source in paths)
        {
            if (Directory.Exists(source))
            {
                string sourceFull = Path.GetFullPath(source).TrimEnd('\\');
                string targetFull = Path.GetFullPath(targetDirectory).TrimEnd('\\');
                if (string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase) ||
                    targetFull.StartsWith(sourceFull + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        TransferRequested?.Invoke(paths, targetDirectory, false);
        e.Handled = true;
    }

    private static List<string> GetSelectedPaths(IEnumerable<FolderItem> folders)
    {
        var paths = new List<string>();
        foreach (var item in folders)
            if (item.IsSelected)
                paths.Add(item.FullPath);
        return paths;
    }
}