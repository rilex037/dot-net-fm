using System.IO;
using System.Windows.Threading;

namespace DotNetFM;

public sealed class WindowsDirectoryWatcher : IDirectoryWatcher
{
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _debounceTimer;
    private volatile bool _disposed;

    public event Action? DirectoryChanged;

    public WindowsDirectoryWatcher()
    {
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += OnDebounceTick;
    }

    public void Watch(string path)
    {
        Stop();

        try
        {
            string watchPath = WindowsFileProvider.ResolveDirectoryTarget(new DirectoryInfo(path));

            var watcher = new FileSystemWatcher(watchPath)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                InternalBufferSize = 65536
            };

            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnWatcherError;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch { }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        if (_disposed) return;

        _debounceTimer.Dispatcher.Invoke(() =>
        {
            if (_disposed) return;
            DebounceNotify();
        });
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounceTimer.Stop();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        DebounceNotify();
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (_disposed) return;
        DebounceNotify();
    }

    private void DebounceNotify()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        DirectoryChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounceTimer.Tick -= OnDebounceTick;
        Stop();
    }
}
