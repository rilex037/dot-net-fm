using System;
using System.Linq;
using System.Windows;

namespace DotNetFM;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Global module registry — discovers and holds all registered modules.
    /// Initialized once at startup before any windows are created.
    /// </summary>
    public static ModuleRegistry Modules { get; } = new();

    static App()
    {
        // Scan output directory for DotNetFM.Module.*.dll and register them.
        // Modules communicate via Core interfaces only — no source-level coupling.
        Modules.ScanAndRegisterAll();
    }

    public App()
    {
        // Load theme resources BEFORE XAML parsing so StaticResource refs resolve.
        ThemeService.LoadAndApply();

        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Pass raw CLI arg through — no resolution here.
        // The module system handles routing based on the argument.
        // Empty string = no arg = use default module's open behavior.
        string initialPath = e.Args.Length > 0 ? e.Args[0] : "";

        var mainWindow = new MainWindow(initialPath);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
