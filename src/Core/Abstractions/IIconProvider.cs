using System.Windows.Media.Imaging;

namespace DotNetFM;

/// <summary>
/// Provides file/folder icons for a specific backend.
/// </summary>
public interface IIconProvider
{
    /// <summary>
    /// Gets a thumbnail/icon for the given path at the requested pixel size.
    /// Returns null if the icon cannot be loaded.
    /// </summary>
    Task<BitmapSource?> GetThumbnailAsync(string path, int requestedSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a synchronous 32x32 icon for a file. Used for quick display.
    /// </summary>
    BitmapSource? GetIconForFile(string filePath);
}
