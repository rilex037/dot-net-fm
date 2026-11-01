using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

public partial class FileGridView : UserControl, IFileViewContent
{
    public static readonly DependencyProperty FoldersProperty =
        DependencyProperty.Register(nameof(Folders), typeof(IEnumerable), typeof(FileGridView));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(int), typeof(FileGridView),
            new PropertyMetadata(AppStore.ReadInt("tab.iconsize", 64), OnIconSizeChanged));

    public static readonly DependencyProperty InteractionServiceProperty =
        DependencyProperty.Register(nameof(InteractionService), typeof(FileInteractionService), typeof(FileGridView));

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (FileGridView)d;
        int m = (int)e.NewValue >= 128 ? 2 : 4;
        grid.Resources["ItemMargin"] = new Thickness(m);
    }

    public IEnumerable? Folders
    {
        get => (IEnumerable?)GetValue(FoldersProperty);
        set => SetValue(FoldersProperty, value);
    }

    public int IconSize
    {
        get => (int)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public FileInteractionService? InteractionService
    {
        get => (FileInteractionService?)GetValue(InteractionServiceProperty);
        set => SetValue(InteractionServiceProperty, value);
    }

    public ItemsControl ItemsControl => FolderItemsControl;
    public ScrollViewer ScrollViewer => FileScrollViewer;

    public event Action<MouseWheelEventArgs>? MouseWheelPreview;

    public FileGridView()
    {
        InitializeComponent();
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is FolderItem item && InteractionService != null)
        {
            if (e.Key is Key.Enter or Key.Escape)
            {
                InteractionService.HandleRenameKey(item, textBox.Text, e.Key);
                e.Handled = true;
            }
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is FolderItem item && item.IsEditing && InteractionService != null)
        {
            InteractionService.FinalizeRename(item, textBox.Text);
        }
    }

    private void FileScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        MouseWheelPreview?.Invoke(e);

        if (!e.Handled)
        {
            FileScrollViewer.ScrollToVerticalOffset(FileScrollViewer.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }
}
