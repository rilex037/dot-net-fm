using System.IO;
using System.Windows;
using System.Windows.Input;

namespace DotNetFM;

public class DragDropService
{
    private Point _dragStartPoint;
    private bool _dragThresholdArmed;

    public Action<string[], string, bool>? TransferRequested;

    public void ArmDrag(Point screenPosition)
    {
        _dragStartPoint = screenPosition;
        _dragThresholdArmed = true;
    }

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

        var paths = VisualTreeUtility.GetSelectedPaths(folders);
        if (paths.Count == 0) return false;

        var data = new DataObject(DataFormats.FileDrop, paths.ToArray());
        DragDrop.DoDragDrop(dragSource, data, DragDropEffects.Move | DragDropEffects.Copy);
        return true;
    }

    public void HandleDragOver(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.Move | DragDropEffects.Copy;
        }
        e.Handled = true;
    }

    public void HandleDrop(DragEventArgs e, string targetDirectory, bool forceCopy)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths == null || paths.Length == 0) return;

        string? sourceDir = Path.GetDirectoryName(paths[0]);
        if (sourceDir != null &&
            string.Equals(Path.GetFullPath(sourceDir), Path.GetFullPath(targetDirectory), StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
            return;
        }

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

        TransferRequested?.Invoke(paths, targetDirectory, forceCopy);
        e.Handled = true;
    }
}
