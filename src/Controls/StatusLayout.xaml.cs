using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dot_net_fm;

/// <summary>
/// Status bar displaying status text and zoom slider.
/// </summary>
public partial class StatusLayout : UserControl
{
    /// <summary>Raised when the zoom slider changes. The int is the icon pixel size (24, 32, 48, 64, 128, 256).</summary>
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
        int index = Array.IndexOf(IconSizeStepConverter.Sizes, iconSize);
        if (index < 0) index = 3; // fallback to 64px

        _suppressZoomChanged = true;
        ZoomSlider.Value = index;
        _suppressZoomChanged = false;
    }

    private void ZoomSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Jump to nearest step on track click (exclude thumb area)
        var pos = e.GetPosition(ZoomSlider);
        double fraction = pos.X / ZoomSlider.ActualWidth;
        int index = (int)Math.Round(fraction * (IconSizeStepConverter.Sizes.Length - 1));
        index = Math.Clamp(index, 0, IconSizeStepConverter.Sizes.Length - 1);
        ZoomSlider.Value = index;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomSlider == null || _suppressZoomChanged) return;

        int index = (int)Math.Round(ZoomSlider.Value);
        index = Math.Clamp(index, 0, IconSizeStepConverter.Sizes.Length - 1);
        ZoomChanged?.Invoke(IconSizeStepConverter.Sizes[index]);
    }
}
