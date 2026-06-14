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
    private readonly DispatcherTimer _debounceTimer;
    private bool _disposed;

    /// <summary>Fired on file system changes in the watched directory (debounced).</summary>
    public event Action? DirectoryChanged;

    public DirectoryWatcherService()
    {
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += OnDebounceTick;
    }

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
        Stop();
    }
}
