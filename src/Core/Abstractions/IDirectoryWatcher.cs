namespace DotNetFM;

public interface IDirectoryWatcher : IDisposable
{
    event Action? DirectoryChanged;

    void Watch(string path);

    void Stop();
}
