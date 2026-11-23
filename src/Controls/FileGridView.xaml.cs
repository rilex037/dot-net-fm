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

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((FileGridView)d).ApplyItemMetrics((int)e.NewValue);

    private void ApplyItemMetrics(int iconSize)
    {
        bool large = iconSize >= 128;
        var basePadding = Application.Current.TryFindResource("SpacingItemPadding") is Thickness t
            ? t
            : new Thickness(6, 8, 6, 6);

        Resources["ItemMargin"] = new Thickness(large ? 2 : 4);
        Resources["ItemPadding"] = large
            ? new Thickness(2, basePadding.Top, 2, basePadding.Bottom)
            : basePadding;
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

    private readonly FastWheelScroll _wheelScroll = new();

    public FileGridView()
    {
        InitializeComponent();
        ApplyItemMetrics(IconSize);
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
            _wheelScroll.Apply(FileScrollViewer, e);
            e.Handled = true;
        }
    }
}
