using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

public partial class StatusLayout : UserControl
{
    public event Action<int>? ZoomChanged;

    public event Action? SidebarToggleRequested;

    private bool _suppressZoomChanged;

    private bool _isSidebarCollapsed;

    private const string CollapseLeftIcon = "sidebar-collapse-left.svg";
    private const string CollapseRightIcon = "sidebar-collapse-right.svg";

    public StatusLayout()
    {
        InitializeComponent();
        ZoomSlider.PreviewMouseLeftButtonDown += ZoomSlider_MouseLeftButtonDown;
        SidebarToggleIcon.UriSource = new Uri(IconProvider.GetFullPath(CollapseLeftIcon));
    }

    public void SetSidebarCollapsed(bool collapsed)
    {
        _isSidebarCollapsed = collapsed;
        UpdateToggleIcon();
    }

    private void UpdateToggleIcon()
    {
        string icon = _isSidebarCollapsed ? CollapseRightIcon : CollapseLeftIcon;
        SidebarToggleIcon.UriSource = new Uri(IconProvider.GetFullPath(icon));
    }

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        UpdateToggleIcon();
        SidebarToggleRequested?.Invoke();
    }

    public void SetStatus(string status)
    {
        if (StatusTextBlock != null)
            StatusTextBlock.Text = status;
    }

    public void SetZoomForIconSize(int iconSize)
    {
        int index = Array.IndexOf(ZoomSizes.Sizes, iconSize);
        if (index < 0) index = 1;

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
