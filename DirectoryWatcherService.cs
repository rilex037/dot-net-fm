using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace dot_net_fm;

/// <summary>
/// Watches a directory for file system changes (create, delete, rename).
/// Fires <see cref="DirectoryChanged"/> debounced at 300ms to batch bulk operations.
/// Start watching with <see cref="Watch"/>, stop with <see cref="Stop"/>.
/// </summary>
public sealed class DirectoryWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _debounceTimer;
    private bool _disposed;

    /// <summary>Fired on file system changes in the watched directory (debounced).</summary>
    public event Action? DirectoryChanged;

    /// <summary>Starts watching the given directory path. Stops any previous watch.</summary>
    public void Watch(string path)
    {
        Stop();

        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    /// <summary>Stops watching and cancels pending debounce.</summary>
    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;

        _debounceTimer?.Stop();
        _debounceTimer = null;
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
        _debounceTimer?.Stop();

        _debounceTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(300),
            DispatcherPriority.Normal,
            (_, _) =>
            {
                _debounceTimer?.Stop();
                DirectoryChanged?.Invoke();
            },
            Application.Current.Dispatcher);
        _debounceTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
