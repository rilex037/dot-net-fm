using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Sidebar panel that dynamically renders sections contributed by the active module.
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

    /// <summary>
    /// Raised when the user clicks a sidebar item and requests navigation to its path.
    /// </summary>
    public event Action<string>? NavigateRequested;

    public SidebarPanel()
    {
        InitializeComponent();
    }

    private void SidebarItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SidebarItem.Item sidebarItem)
        {
            string targetPath = sidebarItem.Path;
            if (!string.IsNullOrEmpty(targetPath) &&
                (FileProvider?.IsVirtualRoot(targetPath) == true || Directory.Exists(targetPath)))
            {
                NavigateRequested?.Invoke(targetPath);
            }
        }
    }
}
