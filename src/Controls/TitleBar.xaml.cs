using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace dot_net_fm;

/// <summary>
/// Title bar with window title and chrome controls (minimize, maximize, close).
/// </summary>
public partial class TitleBar : UserControl
{
    private Window? _ownerWindow;

    public event Action<string>? TitleChanged;

    public TitleBar()
    {
        InitializeComponent();
        Loaded += TitleBar_Loaded;
    }

    private void TitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        _ownerWindow = Window.GetWindow(this);
        if (_ownerWindow != null)
        {
            _ownerWindow.StateChanged += OwnerWindow_StateChanged;
        }
    }

    private void OwnerWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeIcon();
    }

    /// <summary>
    /// Sets the title bar display text.
    /// </summary>
    public void SetTitle(string title)
    {
        if (TitleTextBlock != null)
            TitleTextBlock.Text = title;
        TitleChanged?.Invoke(title);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_ownerWindow != null)
            _ownerWindow.WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_ownerWindow == null) return;

        _ownerWindow.WindowState = _ownerWindow.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        if (_ownerWindow == null || MaximizeIcon == null) return;

        if (_ownerWindow.WindowState == WindowState.Maximized)
        {
            // Overlapping squares for "Restore" when window is maximized
            MaximizeIcon.Data = Geometry.Parse("M 0,2 H 8 V 10 H 0 Z M 2,0 H 10 V 8 H 8");
        }
        else
        {
            // Single square for "Maximize" when window is normal
            MaximizeIcon.Data = Geometry.Parse("M 0,1 H 10 V 9 H 0 Z");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_ownerWindow != null)
            _ownerWindow.Close();
    }
}
