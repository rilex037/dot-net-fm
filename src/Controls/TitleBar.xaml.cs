using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DotNetFM;

/// <summary>
/// Title bar with window title and chrome controls (minimize, maximize, close).
/// </summary>
public partial class TitleBar : UserControl
{
    private Window? _ownerWindow;

    public TitleBar()
    {
        InitializeComponent();
        Loaded   += (_, _) => Subscribe(Window.GetWindow(this));
        Unloaded += (_, _) => Unsubscribe();
    }

    private void Subscribe(Window? window)
    {
        _ownerWindow = window;
        if (_ownerWindow != null)
            _ownerWindow.StateChanged += OwnerWindow_StateChanged;
    }

    private void Unsubscribe()
    {
        if (_ownerWindow != null)
            _ownerWindow.StateChanged -= OwnerWindow_StateChanged;
    }

    private void OwnerWindow_StateChanged(object? sender, EventArgs e) =>
        UpdateMaximizeIcon();

    /// <summary>Sets the title bar display text.</summary>
    public void SetTitle(string title) =>
        TitleTextBlock.Text = title;

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        _ownerWindow!.WindowState = WindowState.Minimized;

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e) =>
        _ownerWindow!.WindowState = _ownerWindow.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void UpdateMaximizeIcon()
    {
        if (MaximizeIcon == null) return;
        MaximizeIcon.Data = _ownerWindow?.WindowState == WindowState.Maximized
            ? Geometry.Parse("M 0,2 H 8 V 10 H 0 Z M 2,0 H 10 V 8 H 8")
            : Geometry.Parse("M 0,1 H 10 V 9 H 0 Z");
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        _ownerWindow?.Close();
}
