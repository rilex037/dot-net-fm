using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace DotNetFM;

/// <summary>
/// Menu bar — uses native WPF Menu/MenuItem so popup lifecycle,
/// keyboard navigation (arrows, Escape, mnemonics), and focus are
/// handled entirely by the framework. No manual popup wrangling needed.
///
/// Edit menu commands are bound to <see cref="CommandIds"/> via XAML,
/// so keyboard gesture text is never duplicated.
/// </summary>
public partial class MenuBar : UserControl
{
    public MenuBar()
    {
        InitializeComponent();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version?.ToString(3) ?? "0.0.1";

        MessageBox.Show(
            $"DotNetFM File Manager\nVersion {versionText}-alpha\n\nA lightweight file manager built with WPF.",
            "About DotNetFM",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
