using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dot_net_fm;

/// <summary>
/// Grid view for displaying files and folders with interaction handling.
/// Layout sizing is handled natively via XAML Data Binding.
/// </summary>
public partial class FileGridView : UserControl
{
    public static readonly DependencyProperty FoldersProperty =
        DependencyProperty.Register(nameof(Folders), typeof(ObservableCollection<FolderItem>), typeof(FileGridView));

    public static readonly DependencyProperty InteractionServiceProperty =
        DependencyProperty.Register(nameof(InteractionService), typeof(FileInteractionService), typeof(FileGridView));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(int), typeof(FileGridView), 
            new PropertyMetadata(64));

    public ObservableCollection<FolderItem>? Folders
    {
        get => (ObservableCollection<FolderItem>?)GetValue(FoldersProperty);
        set => SetValue(FoldersProperty, value);
    }

    public FileInteractionService? InteractionService
    {
        get => (FileInteractionService?)GetValue(InteractionServiceProperty);
        set => SetValue(InteractionServiceProperty, value);
    }

    public int IconSize
    {
        get => (int)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly DependencyProperty CurrentPathProperty =
        DependencyProperty.Register(nameof(CurrentPath), typeof(string), typeof(FileGridView),
            new PropertyMetadata(""));

    public string CurrentPath
    {
        get => (string)GetValue(CurrentPathProperty);
        set => SetValue(CurrentPathProperty, value);
    }

    public FileGridView()
    {
        InitializeComponent();
    }

    private void ItemBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {

        if (e.OriginalSource is TextBlock)
            return;

        if (sender is not FrameworkElement element || element.DataContext is not FolderItem folderItem)
            return;

        if (folderItem.IsEditing)
        {
            e.Handled = true;
            return;
        }

        InteractionService?.HandleIconMouseDown(folderItem, FolderItemsControl, e);
        e.Handled = true;
    }

    private void NameText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not FolderItem folderItem)
            return;

        if (folderItem.IsEditing)
        {
            e.Handled = true;
            return;
        }

        InteractionService?.HandleNameMouseDown(folderItem, FolderItemsControl, e);
        e.Handled = true;
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is FolderItem item)
        {
            InteractionService?.HandleRenameKeyDown(e, textBox, item);
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is FolderItem item)
        {
            InteractionService?.CommitRename(textBox, item);
        }
    }

    // --- Rubber band selection ---

    private void FileView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FileScrollViewer != null)
        {
            var clickPos = e.GetPosition(FileScrollViewer);
            double vsWidth = FileScrollViewer.ViewportWidth;
            if (clickPos.X >= vsWidth - 10 && clickPos.Y > 0)
                return;
        }

        if (InteractionService == null) return;

        if (InteractionService.IsClickOnItem(e, FolderItemsControl))
        {
            var hitItem = InteractionService.GetItemAtPosition(e, FolderItemsControl);
            if (hitItem != null && hitItem.IsEditing)
                return;

            Focus();

            var folders = Folders ?? new ObservableCollection<FolderItem>();
            InteractionService.CommitActiveRename(folders, FolderItemsControl);
            return;
        }

        Focus();

        var allFolders = Folders ?? new ObservableCollection<FolderItem>();
        InteractionService.CommitActiveRename(allFolders, FolderItemsControl);
        InteractionService.ClearAllSelections(allFolders);

        var pos = e.GetPosition(SelectionCanvas);
        InteractionService.HandleRubberBandMouseDown(pos, SelectionCanvas, SelectionBorder);
    }

    private void FileView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (InteractionService == null) return;

        if (Folders != null)
            InteractionService.UpdateDrag(this, Folders);

        var pos = e.GetPosition(SelectionCanvas);
        InteractionService.HandleRubberBandMouseMove(pos, SelectionCanvas, SelectionBorder);

        if (InteractionService.IsRubberBanding && Folders != null)
        {
            InteractionService.UpdateRubberBandSelection(Folders, FolderItemsControl, SelectionCanvas, SelectionBorder);
        }
    }

    private void FileView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        InteractionService?.HandleRubberBandMouseUp(SelectionCanvas, SelectionBorder);
    }

    /// <summary>
    /// Handles right-click: shows native shell context menu for selected items.
    /// If clicking on an unselected item, selects it first.
    /// </summary>
    private void FileView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (InteractionService == null) return;

        var clickedItem = InteractionService.GetItemAtPosition(e, FolderItemsControl);

        if (clickedItem != null && Folders != null)
        {
            // If the clicked item is not part of the current selection, select only it
            if (!clickedItem.IsSelected)
            {
                InteractionService.ClearAllSelections(Folders);
                clickedItem.IsSelected = true;
            }

            // Collect all selected item paths
            var selectedPaths = new List<string>();
            foreach (var item in Folders)
            {
                if (item.IsSelected)
                    selectedPaths.Add(item.FullPath);
            }

            if (selectedPaths.Count > 0)
            {
                // Get screen position for the context menu
                var screenPos = PointToScreen(e.GetPosition(this));
                InteractionService.ContextMenuRequested?.Invoke(screenPos, selectedPaths);
            }
        }
        else
        {
            // Right-click on empty space: show context menu for the folder itself
            // (optional — could be used for "New > Folder" etc.)
            // For now, do nothing on empty space right-click.
        }
    }

    public event Action<MouseWheelEventArgs>? MouseWheelPreview;

    private void FileScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        MouseWheelPreview?.Invoke(e);

        if (!e.Handled)
        {
            FileScrollViewer.ScrollToVerticalOffset(FileScrollViewer.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }

    private void FileView_DragOver(object sender, DragEventArgs e)
    {
        InteractionService?.HandleDragOver(e);
    }

    private void FileView_Drop(object sender, DragEventArgs e)
    {
        if (InteractionService == null) return;

        var pos = e.GetPosition(FolderItemsControl);
        var hitItem = InteractionService.GetItemAtPoint(pos, FolderItemsControl);
        string targetDir = (hitItem != null && hitItem.IsFolder) ? hitItem.FullPath : CurrentPath;

        InteractionService.HandleDrop(e, targetDir);
    }

}
