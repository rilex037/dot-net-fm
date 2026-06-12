using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FmDn;

/// <summary>
/// Status bar displaying status text and zoom slider.
/// </summary>
public partial class StatusLayout : UserControl
{
    /// <summary>Raised when the zoom slider changes. The int is the icon pixel size (24, 32, 48, 64, 128, 256).</summary>
    public event Action<int>? ZoomChanged;

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
        if (ZoomSlider == null) return;

        int index = (int)Math.Round(ZoomSlider.Value);
        index = Math.Clamp(index, 0, IconSizeStepConverter.Sizes.Length - 1);
        ZoomChanged?.Invoke(IconSizeStepConverter.Sizes[index]);
    }
}
