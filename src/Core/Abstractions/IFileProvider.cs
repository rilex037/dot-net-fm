namespace DotNetFM;

/// <summary>
/// Provides file listing and metadata for a specific backend (local FS, FTP, cloud, etc.).
/// Supports lazy loading via range/offset pagination so the client can fetch exactly
/// the items it needs.
/// </summary>
public interface IFileProvider
{
    /// <summary>
    /// Loads a page of file/folder items at the given path.
    /// </summary>
    /// <param name="path">The directory path to list contents of.</param>
    /// <param name="offset">Zero-based index of the first item to return.</param>
    /// <param name="count">Maximum number of items to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result containing the items and total count.</returns>
    Task<FileResult> GetItemsAsync(string path, int offset, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the display title for a given path (e.g., folder name, "My Computer").
    /// </summary>
    string GetDisplayTitle(string path);

    /// <summary>
    /// Gets the human-readable display path for the address bar.
    /// Should NOT include the module URI prefix.
    /// </summary>
    string GetDisplayPath(string path);

    /// <summary>
    /// Gets the parent path, or null if at root.
    /// </summary>
    string? GetParentPath(string path);

    /// <summary>
    /// Returns true if the path represents a "virtual" location (like "My Computer")
    /// that cannot have a parent.
    /// </summary>
    bool IsVirtualRoot(string path);

    /// <summary>
    /// Gets free space info for the path if applicable, or null.
    /// </summary>
    string? GetFreeSpaceInfo(string path);
}
