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

        // Initialize key-value store — bootstraps .default + .{user} files.
        AppStore.Init(AppContext.BaseDirectory);

        // Join all CLI args — handles unquoted paths with spaces from shell launch.
        // A file manager always receives a single path; OS may split on spaces if unquoted.
        string initialPath = e.Args.Length > 0 ? string.Join(" ", e.Args) : "";

        var mainWindow = new MainWindow(initialPath);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
