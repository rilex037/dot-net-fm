using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Grid view for displaying files and folders.
/// Pure rendering — all interaction logic lives in <see cref="FileViewContainer"/>.
/// Implements <see cref="IFileViewContent"/> so the container can host it.
/// </summary>
public partial class FileGridView : UserControl, IFileViewContent
{
    // ── Dependency properties ─────────────────────────────────────

    public static readonly DependencyProperty FoldersProperty =
        DependencyProperty.Register(nameof(Folders), typeof(IEnumerable), typeof(FileGridView));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(int), typeof(FileGridView),
            new PropertyMetadata(int.Parse(AppStore.Read("tab.iconsize")), OnIconSizeChanged));

    public static readonly DependencyProperty InteractionServiceProperty =
        DependencyProperty.Register(nameof(InteractionService), typeof(FileInteractionService), typeof(FileGridView));

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (FileGridView)d;
        int m = (int)e.NewValue >= 128 ? 12 : 4;
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

    // ── IFileViewContent ─────────────────────────────────────────

    public ItemsControl ItemsControl => FolderItemsControl;
    public ScrollViewer ScrollViewer => FileScrollViewer;

    public event Action<MouseWheelEventArgs>? MouseWheelPreview;

    // ── Constructor ──────────────────────────────────────────────

    public FileGridView()
    {
        InitializeComponent();
    }

    // ── Rename textbox events (view-specific, forward to service) ──

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

    // ── Mouse wheel (custom scroll + forward zoom to container) ──

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