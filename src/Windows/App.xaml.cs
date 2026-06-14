using System;
using System.Linq;
using System.Windows;

namespace dot_net_fm;

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

        // Parse CLI arguments. Windows shell passes folder path when "Open with" is used.
        // Format: DotNetFM.exe [path_or_uri]
        // Path can be a bare path like C:\Users or a module URI like windows://C:\Users
        string initialPath = ResolveInitialPath(e.Args);

        var mainWindow = new MainWindow(initialPath);
        mainWindow.Show();
    }

    /// <summary>
    /// Resolves the initial path from CLI args. Falls back to the first module's
    /// default URI prefix + desktop.
    /// </summary>
    private static string ResolveInitialPath(string[] args)
    {
        // Check if a valid path was passed as CLI arg (standard Windows "Open with")
        if (args.Length > 0)
        {
            var arg = args[0];
            if (!string.IsNullOrWhiteSpace(arg))
            {
                // If it's already a module URI (e.g. windows://C:\Users), use it directly
                if (arg.Contains("://", StringComparison.Ordinal))
                    return arg;

                // Otherwise it's a bare filesystem path — resolve it to a module URI
                var module = Modules.FindByPath(arg);
                if (module != null)
                    return $"{module.UriPrefix}://{arg}";

                return arg;
            }
        }

        // No CLI arg — use first module's default with the Desktop path
        return "windows://";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}