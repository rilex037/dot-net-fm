using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DotNetFM;

public partial class SidebarPanel : UserControl
{
    public static readonly DependencyProperty SectionsProperty =
        DependencyProperty.Register(nameof(Sections), typeof(ObservableCollection<SidebarSectionView>), typeof(SidebarPanel));

    public static readonly DependencyProperty FileProviderProperty =
        DependencyProperty.Register(nameof(FileProvider), typeof(IFileProvider), typeof(SidebarPanel));

    public ObservableCollection<SidebarSectionView>? Sections
    {
        get => (ObservableCollection<SidebarSectionView>?)GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }

    public IFileProvider? FileProvider
    {
        get => (IFileProvider?)GetValue(FileProviderProperty);
        set => SetValue(FileProviderProperty, value);
    }

    public SidebarSectionView? BookmarksSection { get; set; }

    private SidebarEventHandler? _handler;
    private ItemsControl? _sectionsControl;

    public event Action<string>? NavigateRequested;

    public event Action<string>? OpenInNewTabRequested;

    public event Action<SidebarItem.Item, Point>? BookmarkContextMenuRequested;

    public event Action<Point>? BookmarksSectionContextMenuRequested;

    public event Action<IReadOnlyList<string>>? BookmarksDropped;

    public SidebarPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewMouseRightButtonUp += OnPreviewMouseRightButtonUp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_handler != null) return;

        _sectionsControl = VisualTreeUtility.FindDescendant<ItemsControl>(this);
        if (_sectionsControl == null) return;

        _handler = new SidebarEventHandler(_sectionsControl, FileProvider);
        _handler.NavigateRequested += path => NavigateRequested?.Invoke(path);
        _handler.OpenInNewTabRequested += path => OpenInNewTabRequested?.Invoke(path);
    }

    private void OnSectionHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (sender is DependencyObject d)
        {
            var section = VisualTreeUtility.FindDataContext<SidebarSectionView>(d);
            section?.ToggleCollapsed();
        }
    }

    private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (BookmarksSection == null || _sectionsControl == null) return;

        var pos = e.GetPosition(this);
        if (!IsOverSection(BookmarksSection, pos)) return;

        var item = VisualTreeUtility.GetSidebarItemAtPoint(_sectionsControl, e.GetPosition(_sectionsControl));
        if (item != null && item.Tag is BookmarkEntry)
        {
            BookmarkContextMenuRequested?.Invoke(item, PointToScreen(e.GetPosition(this)));
        }
        else
        {
            BookmarksSectionContextMenuRequested?.Invoke(PointToScreen(e.GetPosition(this)));
        }

        e.Handled = true;
    }

    private bool IsOverSection(SidebarSectionView section, Point position)
    {
        var hit = VisualTreeHelper.HitTest(this, position);
        if (hit == null) return false;

        DependencyObject? obj = hit.VisualHit;
        while (obj != null && !ReferenceEquals(obj, this))
        {
            if (obj is ContentPresenter cp && ReferenceEquals(cp.DataContext, section))
                return true;
            obj = VisualTreeHelper.GetParent(obj);
        }

        return false;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (BookmarksSection != null &&
            e.Data.GetDataPresent(DataFormats.FileDrop) &&
            IsOverSection(BookmarksSection, e.GetPosition(this)))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (BookmarksSection == null) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (!IsOverSection(BookmarksSection, e.GetPosition(this))) return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        var folders = paths.Where(Directory.Exists).ToList();
        if (folders.Count > 0)
            BookmarksDropped?.Invoke(folders);

        e.Handled = true;
    }
}
