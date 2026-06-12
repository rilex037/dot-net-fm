using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FmDn;

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

        if (_ownerWindow.WindowState == WindowState.Maximized)
        {
            _ownerWindow.WindowState = WindowState.Normal;
            // Draw a single square for "Maximize" since the window is now normal
            MaximizeIcon.Data = Geometry.Parse("M 0,1 H 10 V 9 H 0 Z");
        }
        else
        {
            _ownerWindow.WindowState = WindowState.Maximized;
            // Draw overlapping squares for "Restore" since the window is now maximized
            MaximizeIcon.Data = Geometry.Parse("M 0,2 H 8 V 10 H 0 Z M 2,0 H 10 V 8 H 8");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_ownerWindow != null)
            _ownerWindow.Close();
    }
}
