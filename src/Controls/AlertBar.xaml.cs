using System;
using System.Windows;
using System.Windows.Controls;

namespace DotNetFM;

/// <summary>
/// Reusable Nemo-style alert overlay. Shows an error icon, title, message,
/// and OK/dismiss button. Place as an overlay in a Grid and call ShowAlert/HideAlert.
/// </summary>
public partial class AlertBar : UserControl
{
    public event Action? Dismissed;

    public AlertBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Displays the alert with the given title and message.
    /// </summary>
    public void ShowAlert(string title, string message)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides the alert.
    /// </summary>
    public void HideAlert()
    {
        Visibility = Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        HideAlert();
        Dismissed?.Invoke();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideAlert();
        Dismissed?.Invoke();
    }
}