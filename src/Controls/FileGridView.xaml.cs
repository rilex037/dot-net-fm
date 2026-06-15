using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DotNetFM;

/// <summary>
/// Grid view for displaying files and folders with interaction handling.
/// Owns all visual-tree concerns (rubber band rect rendering, TextBox focus, hit-testing).
/// Delegates logic to <see cref="FileInteractionService"/> which composes pure services.
/// </summary>
public partial class FileGridView : UserControl
{
    public static readonly DependencyProperty FoldersProperty =
        DependencyProperty.Register(nameof(Folders), typeof(IEnumerable), typeof(FileGridView));

    public static readonly DependencyProperty InteractionServiceProperty =
        DependencyProperty.Register(nameof(InteractionService), typeof(FileInteractionService), typeof(FileGridView));

    public static readonly DependencyProperty DragDropServiceProperty =
        DependencyProperty.Register(nameof(DragDropService), typeof(DragDropService), typeof(FileGridView));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(int), typeof(FileGridView),
            new PropertyMetadata(64, OnIconSizeChanged));

    public static readonly DependencyProperty ItemMarginProperty =
        DependencyProperty.Register(nameof(ItemMargin), typeof(Thickness), typeof(FileGridView),
            new PropertyMetadata(new Thickness(4)));

    public Thickness ItemMargin
    {
        get => (Thickness)GetValue(ItemMarginProperty);
        set => SetValue(ItemMarginProperty, value);
    }

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (FileGridView)d;
        int m = (int)e.NewValue >= 128 ? 8 : 4;
        grid.ItemMargin = new Thickness(m);
    }

    public IEnumerable? Folders
    {
        get => (IEnumerable?)GetValue(FoldersProperty);
        set => SetValue(FoldersProperty, value);
    }

    public FileInteractionService? InteractionService
    {
        get => (FileInteractionService?)GetValue(InteractionServiceProperty);
        set => SetValue(InteractionServiceProperty, value);
    }

    public DragDropService? DragDropService
    {
        get => (DragDropService?)GetValue(DragDropServiceProperty);
        set => SetValue(DragDropServiceProperty, value);
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

    // ── Rubber band state ─────────────────────────────────────────

    private bool _isRubberBanding;
    private Point _rubberBandStart;

    // ── Constructor ───────────────────────────────────────────────

    public FileGridView()
    {
        InitializeComponent();
    }

    // ── Item click handling ───────────────────────────────────────

    private void ItemBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not FolderItem folderItem)
            return;

        if (folderItem.IsEditing)
        {
            e.Handled = true;
            return;
        }

        bool isNameClick = e.OriginalSource is TextBlock;

        HandleItemClick(folderItem, isNameClick);
        DragDropService?.ArmDrag(e.GetPosition(null));
        e.Handled = true;
    }

    private void HandleItemClick(FolderItem clickedItem, bool isNameClick)
    {
        if (InteractionService == null) return;

        InteractionService.HandleItemMouseDown(clickedItem, isNameClick, folders =>
        {
            ClearAllSelections();
        });
    }

    // ── Rename textbox events ─────────────────────────────────────

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is FolderItem item && InteractionService != null)
        {
            InteractionService.HandleRenameKey(item, textBox.Text, e.Key);
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is FolderItem item && InteractionService != null)
        {
            InteractionService.FinalizeRename(item, textBox.Text);
        }
    }

    // ── Rubber band selection ─────────────────────────────────────

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

        var hitItem = VisualTreeUtility.GetFolderItemAtPoint(FolderItemsControl, e.GetPosition(FolderItemsControl));
        if (hitItem != null)
        {
            if (hitItem.IsEditing)
                return;

            Focus();
            CommitAnyRename();
            return;
        }

        Focus();
        CommitAnyRename();
        ClearAllSelections();

        _rubberBandStart = e.GetPosition(SelectionCanvas);
        _isRubberBanding = false;

        SelectionCanvas.CaptureMouse();
        SelectionBorder.Visibility = Visibility.Collapsed;
    }

    private void FileView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (InteractionService == null) return;

        var typedFolders = Folders as IEnumerable<FolderItem>;
        if (typedFolders != null)
            DragDropService?.UpdateDrag(this, typedFolders);

        if (Mouse.Captured != SelectionCanvas) return;

        var current = e.GetPosition(SelectionCanvas);

        if (!_isRubberBanding)
        {
            if (RubberBandHelper.IsBeyondThreshold(_rubberBandStart, current))
            {
                _isRubberBanding = true;
                SelectionBorder.Visibility = Visibility.Visible;
            }
        }

        if (!_isRubberBanding) return;

        var rect = RubberBandHelper.ComputeSelectionRect(_rubberBandStart, current);

        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;

        if (typedFolders != null)
        {
            RubberBandHelper.ApplySelection(typedFolders, rect, item =>
            {
                var container = FolderItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container == null) return null;
                var topLeft = container.TranslatePoint(new Point(0, 0), SelectionCanvas);
                return new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));
            });
        }
    }

    private void FileView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured != SelectionCanvas) return;

        SelectionCanvas.ReleaseMouseCapture();
        SelectionBorder.Visibility = Visibility.Collapsed;
        _isRubberBanding = false;
    }

    // ── Right-click context menu ──────────────────────────────────

    private void FileView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (InteractionService == null) return;

        var hitItem = VisualTreeUtility.GetFolderItemAtPoint(FolderItemsControl, e.GetPosition(FolderItemsControl));
        var typedFolders = Folders as IEnumerable<FolderItem>;

        if (hitItem != null && typedFolders != null)
        {
            if (!hitItem.IsSelected)
            {
                ClearAllSelections();
                hitItem.IsSelected = true;
            }

            var selectedPaths = new List<string>();
            foreach (var item in typedFolders)
            {
                if (item.IsSelected)
                    selectedPaths.Add(item.FullPath);
            }

            if (selectedPaths.Count > 0)
            {
                var screenPos = PointToScreen(e.GetPosition(this));
                InteractionService.ContextMenuRequested?.Invoke(screenPos, selectedPaths);
            }
        }
    }

    // ── Rename commit helpers ─────────────────────────────────────

    /// <summary>
    /// Commits the active rename if one exists, locating the TextBox from the active editing item.
    /// </summary>
    public void CommitAnyRename()
    {
        if (InteractionService == null) return;

        InteractionService.CommitPendingRename(
            clearAllSelections: folders => ClearAllSelections(),
            onCommitted: item =>
            {
                var container = FolderItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                var textBox = container != null ? VisualTreeUtility.FindDescendant<TextBox>(container) : null;
                if (textBox != null)
                    InteractionService?.FinalizeRename(item, textBox.Text);
            });
    }

    /// <summary>
    /// Begins rename on the given item, focusing the TextBox and selecting the appropriate text.
    /// Should be called after the rename mode has been set on the item.
    /// </summary>
    public void FocusRenameTextBox(FolderItem item)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var container = FolderItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            var textBox = container != null ? VisualTreeUtility.FindDescendant<TextBox>(container) : null;
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

    // ── Selection helpers ─────────────────────────────────────────

    public void ClearAllSelections()
    {
        if (Folders is IEnumerable<FolderItem> folders)
        {
            foreach (var item in folders)
                item.IsSelected = false;
        }
    }

    // ── Mouse wheel / zoom ────────────────────────────────────────

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

    // ── Drag and drop ─────────────────────────────────────────────

    private void FileView_DragOver(object sender, DragEventArgs e)
    {
        DragDropService?.HandleDragOver(e);
    }

    private void FileView_Drop(object sender, DragEventArgs e)
    {
        if (DragDropService == null || InteractionService == null) return;

        var pos = e.GetPosition(FolderItemsControl);
        var hitItem = VisualTreeUtility.GetFolderItemAtPoint(FolderItemsControl, pos);
        string targetDir = (hitItem != null && hitItem.IsFolder) ? hitItem.FullPath : CurrentPath;

        DragDropService.HandleDrop(e, targetDir);
    }
}
