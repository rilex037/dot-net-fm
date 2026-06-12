using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace FmDn;

/// <summary>
/// Menu bar — uses native WPF Menu/MenuItem so popup lifecycle,
/// keyboard navigation (arrows, Escape, mnemonics), and focus are
/// handled entirely by the framework. No manual popup wrangling needed.
/// </summary>
public partial class MenuBar : UserControl
{
    /// <summary>Raised when Edit → Rename is clicked.</summary>
    public event Action? RenameRequested;

    /// <summary>Raised when Edit → Delete is clicked.</summary>
    public event Action? DeleteRequested;

    public MenuBar()
    {
        InitializeComponent();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) =>
        Window.GetWindow(this)?.Close();

    private void Rename_Click(object sender, RoutedEventArgs e) =>
        RenameRequested?.Invoke();

    private void Delete_Click(object sender, RoutedEventArgs e) =>
        DeleteRequested?.Invoke();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version?.ToString(3) ?? "0.0.1";

        MessageBox.Show(
            $"FmDn File Manager\nVersion {versionText}-alpha\n\nA lightweight file manager built with WPF.",
            "About FmDn",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}