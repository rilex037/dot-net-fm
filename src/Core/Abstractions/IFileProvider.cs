namespace DotNetFM;

public interface IFileProvider
{
    Task<FileResult> GetItemsAsync(string path, int offset, int count, string sortKey, bool sortAscending, CancellationToken cancellationToken = default);

    string GetDisplayTitle(string path);

    string GetDisplayPath(string path);

    string? GetParentPath(string path);

    bool IsVirtualRoot(string path);

    bool PathExists(string path);

    string? GetFreeSpaceInfo(string path);
}
