using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows;

namespace DotNetFM;

public partial class App : Application
{
    private const string MutexName = @"Local\DotNetFM.SingleInstance";
    private const string PipeName = "DotNetFM.SingleInstance";

    public static ModuleRegistry Modules { get; } = new();

    private static Mutex? _instanceMutex;
    private static bool _ownsInstanceMutex;

    static App()
    {
        Modules.ScanAndRegisterAll();
    }

    public App()
    {
        ThemeService.LoadAndApply();
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(true, MutexName, out bool isFirstInstance);
        _ownsInstanceMutex = isFirstInstance;

        var request = LaunchRequest.Parse(e.Args);

        if (!isFirstInstance)
        {
            ForwardToRunningInstance(request.ToWire());
            Shutdown();
            return;
        }

        AppStore.Init(AppContext.BaseDirectory);

        DispatcherUnhandledException += (_, args) =>
        {
            CustomDialog.Show("Unexpected Error", args.Exception.Message, DialogIcon.Error);
            args.Handled = true;
        };

        var mainWindow = new MainWindow(request.Kind == LaunchRequestKind.Open ? request.Path : "");
        MainWindow = mainWindow;
        mainWindow.Show();

        if (request.Kind == LaunchRequestKind.Select)
            mainWindow.HandleLaunchRequest(request);

        StartPipeServer(mainWindow);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_ownsInstanceMutex)
                _instanceMutex?.ReleaseMutex();
        }
        catch
        {
        }
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }

    private static void ForwardToRunningInstance(string args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine(args);
        }
        catch
        {
        }
    }

    private static void StartPipeServer(MainWindow window)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    server.WaitForConnection();

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    string? line = reader.ReadLine();

                    if (line == null) continue;

                    var dispatcher = Current?.Dispatcher;
                    var request = LaunchRequest.FromWire(line);
                    dispatcher?.BeginInvoke(() => window.HandleLaunchRequest(request));
                }
                catch
                {
                    return;
                }
            }
        })
        {
            IsBackground = true
        };
        thread.Start();
    }
}
