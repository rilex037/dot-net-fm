using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DotNetFM;

/// <summary>
/// Container that owns ALL interaction logic (selection, rubber band, click dispatch,
/// rename, drag-drop) and hosts a swappable child view for rendering.
/// Child views implement <see cref="IFileViewContent"/> and only provide layout/template.
/// </summary>
public partial class FileViewContainer : UserControl, IFileView
{
    // ── Dependency properties ─────────────────────────────────────

    public static readonly DependencyProperty FoldersProperty =
        DependencyProperty.Register(nameof(Folders), typeof(IEnumerable), typeof(FileViewContainer));

    public static readonly DependencyProperty InteractionServiceProperty =
        DependencyProperty.Register(nameof(InteractionService), typeof(FileInteractionService), typeof(FileViewContainer));

    public static readonly DependencyProperty DragDropServiceProperty =
        DependencyProperty.Register(nameof(DragDropService), typeof(DragDropService), typeof(FileViewContainer));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(int), typeof(FileViewContainer));

    public static readonly DependencyProperty CurrentPathProperty =
        DependencyProperty.Register(nameof(CurrentPath), typeof(string), typeof(FileViewContainer),
            new PropertyMetadata(""));

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

    public string CurrentPath
    {
        get => (string)GetValue(CurrentPathProperty);
        set => SetValue(CurrentPathProperty, value);
    }

    // ── Active child view ────────────────────────────────────────

    private IFileViewContent? _activeContent;

    /// <summary>Sets the active child view for rendering.</summary>
    public void SetActiveContent(IFileViewContent content)
    {
        if (_activeContent != null)
            UnsubscribeContentEvents(_activeContent);

        _activeContent = content;
        if (content is UIElement element)
            ContentHost.Content = element;

        SubscribeContentEvents(content);
        ForwardStateToContent();
    }

    private ItemsControl? ActiveItemsControl => _activeContent?.ItemsControl;
    private ScrollViewer? ActiveScrollViewer => _activeContent?.ScrollViewer;

    private void SubscribeContentEvents(IFileViewContent content)
    {
        content.MouseWheelPreview += OnContentMouseWheelPreview;
    }

    private void UnsubscribeContentEvents(IFileViewContent content)
    {
        content.MouseWheelPreview -= OnContentMouseWheelPreview;
    }

    private void OnContentMouseWheelPreview(MouseWheelEventArgs e)
    {
        MouseWheelPreview?.Invoke(e);
    }

    private void ForwardStateToContent()
    {
        if (_activeContent is FileGridView grid)
        {
            grid.Folders = Folders;
            grid.IconSize = IconSize;
            grid.InteractionService = InteractionService;
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        ForwardStateToContent();
    }

    // ── Selection state ──────────────────────────────────────────

    private FolderItem? _anchorItem;
    private HashSet<FolderItem>? _preRubberBandSnapshot;

    // ── Rubber band state ────────────────────────────────────────

    private bool _isRubberBanding;
    private Point _rubberBandStart;

    // ── Constructor ──────────────────────────────────────────────

    public FileViewContainer()
    {
        InitializeComponent();
        PreviewMouseDown += FileViewContainer_PreviewMouseDown;
    }

    private void FileViewContainer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || e.ButtonState != MouseButtonState.Pressed)
            return;

        var hitItem = GetHitItem(e);
        if (hitItem is { IsFolder: true })
        {
            OpenInNewTabRequested?.Invoke(hitItem.FullPath);
            e.Handled = true;
        }
    }

    // ── Hit testing helper ───────────────────────────────────────

    private FolderItem? GetHitItem(MouseEventArgs e)
    {
        if (ActiveItemsControl == null) return null;
        return VisualTreeUtility.GetFolderItemAtPoint(ActiveItemsControl, e.GetPosition(ActiveItemsControl));
    }

    // ── Item click handling ──────────────────────────────────────

    private void HandleItemClick(FolderItem clickedItem, bool isNameClick, ModifierKeys modifiers)
    {
        if (InteractionService == null) return;

        if (modifiers.HasFlag(ModifierKeys.Shift) && _anchorItem != null)
        {
            // Range select from anchor to clicked item — no click timing
            SelectRange(clickedItem);
        }
        else if (modifiers.HasFlag(ModifierKeys.Control))
        {
            // Toggle selection — no click timing
            clickedItem.IsSelected = !clickedItem.IsSelected;
        }
        else
        {
            // Plain click — let InteractionService handle selection + click timing
            InteractionService.HandleItemMouseDown(clickedItem, isNameClick, folders =>
            {
                ClearAllSelections();
            });
        }

        // Anchor is updated on all non-Shift clicks
        if (!modifiers.HasFlag(ModifierKeys.Shift))
            _anchorItem = clickedItem;
    }

    /// <summary>
    /// Selects a contiguous range from the anchor to the target item (inclusive).
    /// </summary>
    private void SelectRange(FolderItem toItem)
    {
        if (Folders is not IEnumerable<FolderItem> items) return;

        var list = items as IList<FolderItem> ?? items.ToList();
        int anchorIdx = -1, toIdx = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], _anchorItem)) anchorIdx = i;
            if (ReferenceEquals(list[i], toItem)) toIdx = i;
        }
        if (anchorIdx < 0 || toIdx < 0) return;

        int start = Math.Min(anchorIdx, toIdx);
        int end = Math.Max(anchorIdx, toIdx);

        ClearAllSelections();
        for (int i = start; i <= end; i++)
            list[i].IsSelected = true;
    }

    // ── Rubber band selection ────────────────────────────────────

    private void FileView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (InteractionService == null) return;

        // Skip rubber band near scrollbar
        var scroll = ActiveScrollViewer;
        if (scroll != null)
        {
            var clickPos = e.GetPosition(scroll);
            double vsWidth = scroll.ViewportWidth;
            if (clickPos.X >= vsWidth - 10 && clickPos.Y > 0)
                return;
        }

        var hitItem = GetHitItem(e);
        if (hitItem != null)
        {
            if (hitItem.IsEditing)
                return;

            Focus();
            CommitAnyRename();

            // Handle item click for selection + click timing (rename/open)
            bool isNameClick = e.OriginalSource is TextBlock;
            HandleItemClick(hitItem, isNameClick, Keyboard.Modifiers);
            DragDropService?.ArmDrag(e.GetPosition(null));
            e.Handled = true;
            return;
        }

        Focus();
        CommitAnyRename();

        // Rubber band — Ctrl held means additive (preserve existing selection)
        bool ctrlHeld = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!ctrlHeld)
            ClearAllSelections();
        SnapshotRubberBandSelection(ctrlHeld);

        _rubberBandStart = e.GetPosition(SelectionCanvas);
        _isRubberBanding = false;

        SelectionCanvas.CaptureMouse();
        SelectionBorder.Visibility = Visibility.Collapsed;
    }

    private void FileView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (InteractionService == null) return;

        if (Folders is IEnumerable<FolderItem> typedFolders)
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

        if (Folders is IEnumerable<FolderItem> selectableFolders && ActiveItemsControl != null)
        {
            var itemsControl = ActiveItemsControl;
            var canvas = SelectionCanvas;
            var snapshot = _preRubberBandSnapshot;
            RubberBandHelper.ApplySelection(selectableFolders, rect, item =>
            {
                if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter container)
                    return null;
                var topLeft = container.TranslatePoint(new Point(0, 0), canvas);
                return new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));
            }, snapshot);
        }
    }

    private void FileView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured != SelectionCanvas) return;

        SelectionCanvas.ReleaseMouseCapture();
        SelectionBorder.Visibility = Visibility.Collapsed;
        _isRubberBanding = false;
        _preRubberBandSnapshot = null;
    }

    // ── Right-click context menu ─────────────────────────────────

    private void FileView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (InteractionService == null) return;

        var hitItem = GetHitItem(e);

        if (hitItem != null && Folders is IEnumerable<FolderItem> typedFolders)
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

    // ── Rename commit helpers ────────────────────────────────────

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
                var textBox = FindRenameBox(item);
                if (textBox != null)
                    InteractionService?.FinalizeRename(item, textBox.Text);
            });
    }

    /// <summary>
    /// Begins rename on the given item, focusing the TextBox and selecting the appropriate text.
    /// </summary>
    public void FocusRenameTextBox(FolderItem item)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var textBox = FindRenameBox(item);
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

    /// <summary>
    /// Finds the rename TextBox for the given item by traversing the active content's visual tree.
    /// </summary>
    private TextBox? FindRenameBox(FolderItem item)
    {
        if (ActiveItemsControl == null) return null;
        if (ActiveItemsControl.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter container)
            return null;
        return VisualTreeUtility.FindDescendant<TextBox>(container);
    }

    // ── Selection helpers ────────────────────────────────────────

    /// <summary>
    /// Snapshots the current selection for Ctrl+rubber band additive mode.
    /// </summary>
    private void SnapshotRubberBandSelection(bool ctrlHeld)
    {
        if (ctrlHeld && Folders is IEnumerable<FolderItem> items)
            _preRubberBandSnapshot = new HashSet<FolderItem>(items.Where(i => i.IsSelected));
        else
            _preRubberBandSnapshot = null;
    }

    public void ClearAllSelections()
    {
        if (Folders is IEnumerable<FolderItem> folders)
        {
            foreach (var item in folders)
                item.IsSelected = false;
        }
    }

    // ── Middle-click → open in new tab ──────────────────────────

    public event Action<string>? OpenInNewTabRequested;

    // ── Mouse wheel / zoom ───────────────────────────────────────

    public event Action<MouseWheelEventArgs>? MouseWheelPreview;

    // ── Drag and drop ────────────────────────────────────────────

    private void FileView_DragOver(object sender, DragEventArgs e)
    {
        DragDropService?.HandleDragOver(e);
    }

    private void FileView_Drop(object sender, DragEventArgs e)
    {
        if (DragDropService == null || InteractionService == null) return;

        var pos = e.GetPosition(ActiveItemsControl!);
        var hitItem = VisualTreeUtility.GetFolderItemAtPoint(ActiveItemsControl!, pos);
        string targetDir = (hitItem != null && hitItem.IsFolder) ? hitItem.FullPath : CurrentPath;

        DragDropService.HandleDrop(e, targetDir);
    }
}