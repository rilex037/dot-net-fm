namespace DotNetFM;

/// <summary>
/// Watches a directory for file system changes and raises events when files are created, deleted, or renamed.
/// </summary>
public interface IDirectoryWatcher : IDisposable
{
    /// <summary>Fired when file system changes are detected in the watched directory (debounced).</summary>
    event Action? DirectoryChanged;

    /// <summary>Starts watching the given directory path. Stops any previous watch.</summary>
    void Watch(string path);

    /// <summary>Stops watching and cancels pending debounce.</summary>
    void Stop();
}
