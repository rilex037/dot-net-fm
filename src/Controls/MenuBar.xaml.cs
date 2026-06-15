using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace dot_net_fm;

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
            $"dot_net_fm File Manager\nVersion {versionText}-alpha\n\nA lightweight file manager built with WPF.",
            "About dot_net_fm",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
