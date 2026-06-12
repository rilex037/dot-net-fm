using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FmDn;

/// <summary>
/// Navigation toolbar with back/forward/up buttons and address bar.
/// Shows the full absolute path and supports typing a new path to navigate on Enter.
/// </summary>
public partial class NavigationToolbar : UserControl
{
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(0xE9, 0x1E, 0x63));
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    public event Action? BackRequested;
    public event Action? ForwardRequested;
    public event Action? UpRequested;
    public event Action<string>? NavigateToRequested;

    public NavigationToolbar()
    {
        InitializeComponent();
        AddressBarTextBox.PreviewKeyDown += AddressBarTextBox_PreviewKeyDown;
    }

    /// <summary>
    /// Updates the address bar with the full path.
    /// </summary>
    public void SetPath(string fullPath)
    {
        if (AddressBarTextBox != null)
            AddressBarTextBox.Text = fullPath;
    }

    /// <summary>
    /// Updates the enabled state of navigation buttons.
    /// </summary>
    public void UpdateNavStates(bool canGoBack, bool canGoForward, bool canGoUp)
    {
        BackButton.IsEnabled = canGoBack;
        BackButton.Cursor = canGoBack ? Cursors.Hand : Cursors.Arrow;

        ForwardButton.IsEnabled = canGoForward;
        ForwardButton.Cursor = canGoForward ? Cursors.Hand : Cursors.Arrow;

        UpButton.IsEnabled = canGoUp;
        UpButton.Cursor = canGoUp ? Cursors.Hand : Cursors.Arrow;
    }

    private void AddressBarTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        AddressBarBorder.BorderBrush = AccentBrush;
    }

    private void AddressBarTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        AddressBarBorder.BorderBrush = TransparentBrush;
    }

    private void AddressBarTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            string text = AddressBarTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(text))
                NavigateToRequested?.Invoke(text);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
    private void ForwardButton_Click(object sender, RoutedEventArgs e) => ForwardRequested?.Invoke();
    private void UpButton_Click(object sender, RoutedEventArgs e) => UpRequested?.Invoke();
}