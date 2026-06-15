using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Status bar displaying status text and zoom slider.
/// </summary>
public partial class StatusLayout : UserControl
{
    /// <summary>Raised when the zoom slider changes. The int is the icon pixel size (48, 64, 128, 256, 512).</summary>
    public event Action<int>? ZoomChanged;

    private bool _suppressZoomChanged;

    public StatusLayout()
    {
        InitializeComponent();
        ZoomSlider.PreviewMouseLeftButtonDown += ZoomSlider_MouseLeftButtonDown;
    }

    /// <summary>
    /// Updates the status text.
    /// </summary>
    public void SetStatus(string status)
    {
        if (StatusTextBlock != null)
            StatusTextBlock.Text = status;
    }

    /// <summary>
    /// Sets the zoom slider to the given icon pixel size without firing <see cref="ZoomChanged"/>.
    /// Used when switching tabs to reflect the target tab's zoom level.
    /// </summary>
    public void SetZoomForIconSize(int iconSize)
    {
        int index = Array.IndexOf(ZoomSizes.Sizes, iconSize);
        if (index < 0) index = 1; // fallback to 64px

        _suppressZoomChanged = true;
        ZoomSlider.Value = index;
        _suppressZoomChanged = false;
    }

    private void ZoomSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(ZoomSlider);
        ZoomSlider.Value = Math.Clamp((int)Math.Round(pos.X / ZoomSlider.ActualWidth * (ZoomSizes.Sizes.Length - 1)), 0, ZoomSizes.Sizes.Length - 1);
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomSlider == null || _suppressZoomChanged) return;

        int index = Math.Clamp((int)Math.Round(ZoomSlider.Value), 0, ZoomSizes.Sizes.Length - 1);
        ZoomChanged?.Invoke(ZoomSizes.Sizes[index]);
    }
}
