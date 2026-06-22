using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Sidebar panel that dynamically renders sections contributed by the active module.
/// Delegates all item interaction to <see cref="SidebarEventHandler"/>.
/// </summary>
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

    private SidebarEventHandler? _handler;

    /// <summary>Raised when the user clicks a sidebar item to navigate in the current tab.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>Raised when the user middle-clicks a sidebar item to open it in a new tab.</summary>
    public event Action<string>? OpenInNewTabRequested;

    public SidebarPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Find the root ItemsControl via visual tree — no x:Name dependency.
        var itemsControl = VisualTreeUtility.FindDescendant<ItemsControl>(this);
        if (itemsControl == null) return;

        _handler = new SidebarEventHandler(itemsControl, FileProvider);
        _handler.NavigateRequested += path => NavigateRequested?.Invoke(path);
        _handler.OpenInNewTabRequested += path => OpenInNewTabRequested?.Invoke(path);
    }

    /// <summary>
    /// Toggles the collapsed state of the section whose header was clicked.
    /// </summary>
    private void OnSectionHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject d)
        {
            var section = VisualTreeUtility.FindDataContext<SidebarSectionView>(d);
            section?.ToggleCollapsed();
        }
    }
}
