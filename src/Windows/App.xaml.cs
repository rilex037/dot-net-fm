using System.Windows;

namespace dot_net_fm;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        // Load theme resources BEFORE XAML parsing so StaticResource refs resolve.
        ThemeService.LoadAndApply();

        InitializeComponent();
    }
}

