using System.Windows.Media.Imaging;

namespace dot_net_fm;

/// <summary>
/// IIconProvider implementation using Windows Shell COM (IShellItemImageFactory)
/// and SHGetFileInfo for native file/folder icons.
/// </summary>
public sealed class WindowsIconProvider : IIconProvider
{
    public Task<BitmapSource?> GetThumbnailAsync(string path, int requestedSize, CancellationToken cancellationToken = default)
    {
        return NativeIconHelper.GetThumbnailAsync(path, requestedSize);
    }

    public BitmapSource? GetIconForFile(string filePath)
    {
        return NativeIconHelper.GetIconForFile(filePath);
    }
}