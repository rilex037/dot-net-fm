using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DotNetFM;

public partial class FileViewContainer : UserControl, IFileView
{
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

    public event Action<Point, string>? BackgroundContextMenuRequested;

    public event Action? SelectionChanged;

    private IFileViewContent? _activeContent;

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

        if (e.Property == FoldersProperty)
            _typeAhead.Reset();
    }

    private FolderItem? _anchorItem;
    private HashSet<FolderItem>? _preRubberBandSnapshot;

    private bool _isRubberBanding;
    private Point _rubberBandStart;

    private FolderItem? _pendingSelectedClick;
    private bool _pendingIsNameClick;
    private Point _pendingClickDownPoint;

    private readonly TypeAheadBuffer _typeAhead = new();

    public FileViewContainer()
    {
        InitializeComponent();
        PreviewMouseDown += FileViewContainer_PreviewMouseDown;
        PreviewTextInput += FileViewContainer_PreviewTextInput;
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

    private FolderItem? GetHitItem(MouseEventArgs e)
    {
        if (ActiveItemsControl == null) return null;
        return VisualTreeUtility.GetFolderItemAtPoint(ActiveItemsControl, e.GetPosition(ActiveItemsControl));
    }

    private void FileViewContainer_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (IsAnyItemEditing()) return;

        foreach (char c in e.Text)
            _typeAhead.Append(c);

        if (_typeAhead.Prefix.Length == 0) return;

        if (TryFindItemByPrefix(_typeAhead.Prefix, out var match))
        {
            SelectSingle(match);
            ScrollItemIntoView(match);
            SelectionChanged?.Invoke();
            e.Handled = true;
        }
    }

    private bool IsAnyItemEditing()
    {
        if (Folders is not IEnumerable<FolderItem> items) return false;
        foreach (var item in items)
            if (item.IsEditing) return true;
        return false;
    }

    private bool TryFindItemByPrefix(string prefix, out FolderItem match)
    {
        if (Folders is IEnumerable<FolderItem> items)
        {
            foreach (var item in items)
            {
                if (item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    match = item;
                    return true;
                }
            }
        }

        match = null!;
        return false;
    }

    private void SelectSingle(FolderItem item)
    {
        ClearAllSelections();
        item.IsSelected = true;
    }

    private void ScrollItemIntoView(FolderItem item)
    {
        if (ActiveItemsControl?.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement container)
            container.BringIntoView();
    }

    public bool SelectItemByPath(string fullPath)
    {
        if (Folders is not IEnumerable<FolderItem> items) return false;

        foreach (var item in items)
        {
            if (!string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)) continue;

            SelectSingle(item);
            ScrollItemIntoView(item);
            SelectionChanged?.Invoke();
            return true;
        }

        return false;
    }

    private void HandleItemClick(FolderItem clickedItem, bool isNameClick, ModifierKeys modifiers)
    {
        if (InteractionService == null) return;

        if (modifiers.HasFlag(ModifierKeys.Shift) && _anchorItem != null)
        {
            SelectRange(clickedItem);
        }
        else if (modifiers.HasFlag(ModifierKeys.Control))
        {
            clickedItem.IsSelected = !clickedItem.IsSelected;
        }
        else
        {
            bool allowRename = !(Folders is IEnumerable<FolderItem> items && items.Count(i => i.IsSelected) > 1);
            InteractionService.HandleItemMouseDown(clickedItem, isNameClick, folders =>
            {
                ClearAllSelections();
            }, allowRename);
        }

        if (!modifiers.HasFlag(ModifierKeys.Shift))
            _anchorItem = clickedItem;

        SelectionChanged?.Invoke();
    }

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

    private void FileView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (InteractionService == null) return;

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

            bool isNameClick = e.OriginalSource is TextBlock;
            if (hitItem.IsSelected)
            {
                _pendingSelectedClick = hitItem;
                _pendingIsNameClick = isNameClick;
                _pendingClickDownPoint = e.GetPosition(null);
            }
            else
            {
                HandleItemClick(hitItem, isNameClick, Keyboard.Modifiers);
            }

            DragDropService?.ArmDrag(e.GetPosition(null));
            e.Handled = true;
            return;
        }

        Focus();
        CommitAnyRename();

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
        {
            bool dragStarted = DragDropService?.UpdateDrag(this, typedFolders) ?? false;
            if (dragStarted)
                _pendingSelectedClick = null;
        }

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
        if (_pendingSelectedClick != null)
        {
            var clicked = _pendingSelectedClick;
            _pendingSelectedClick = null;
            HandleItemClick(clicked, _pendingIsNameClick, Keyboard.Modifiers);
        }

        if (Mouse.Captured != SelectionCanvas) return;

        SelectionCanvas.ReleaseMouseCapture();
        SelectionBorder.Visibility = Visibility.Collapsed;
        _isRubberBanding = false;
        _preRubberBandSnapshot = null;

        SelectionChanged?.Invoke();
    }

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
            return;
        }

        ClearAllSelections();
        SelectionChanged?.Invoke();

        var backgroundPos = PointToScreen(e.GetPosition(this));
        BackgroundContextMenuRequested?.Invoke(backgroundPos, CurrentPath);
        e.Handled = true;
    }

    public void CommitAnyRename()
    {
        if (InteractionService == null) return;

        InteractionService.CommitPendingRename(
            onCommitted: item =>
            {
                var textBox = FindRenameBox(item);
                if (textBox != null)
                    InteractionService?.FinalizeRename(item, textBox.Text);
            });
    }

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

    private TextBox? FindRenameBox(FolderItem item)
    {
        if (ActiveItemsControl == null) return null;
        if (ActiveItemsControl.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter container)
            return null;
        return VisualTreeUtility.FindDescendant<TextBox>(container);
    }

    private void SnapshotRubberBandSelection(bool ctrlHeld)
    {
        if (ctrlHeld && Folders is IEnumerable<FolderItem> items)
            _preRubberBandSnapshot = items.Where(i => i.IsSelected).ToHashSet();
        else
            _preRubberBandSnapshot = null;
    }

    public void ResetScroll(double offset = 0)
    {
        ActiveScrollViewer?.ScrollToVerticalOffset(offset);
    }

    public double VerticalOffset => ActiveScrollViewer?.VerticalOffset ?? 0;

    public void ClearAllSelections()
    {
        if (Folders is IEnumerable<FolderItem> folders)
        {
            foreach (var item in folders)
                item.IsSelected = false;
        }
    }

    public event Action<string>? OpenInNewTabRequested;

    public event Action<string, List<string>>? DropOnFileRequested;

    public event Action<MouseWheelEventArgs>? MouseWheelPreview;

    private void FileView_DragOver(object sender, DragEventArgs e)
    {
        DragDropService?.HandleDragOver(e);
    }

    private void FileView_Drop(object sender, DragEventArgs e)
    {
        if (DragDropService == null || InteractionService == null) return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] droppedPaths || droppedPaths.Length == 0)
            return;

        var pos = e.GetPosition(ActiveItemsControl!);
        var hitItem = VisualTreeUtility.GetFolderItemAtPoint(ActiveItemsControl!, pos);

        if (hitItem is { IsFolder: false })
        {
            DropOnFileRequested?.Invoke(hitItem.FullPath, [.. droppedPaths]);
            e.Handled = true;
            return;
        }

        bool forceCopy = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        string targetDir = hitItem != null ? hitItem.FullPath : CurrentPath;
        DragDropService.HandleDrop(e, targetDir, forceCopy);
    }
}
