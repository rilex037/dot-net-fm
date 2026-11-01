using System.Windows.Media.Imaging;

namespace DotNetFM;

public sealed class WindowsIconProvider : IIconProvider
{
    public Task<BitmapSource?> GetThumbnailAsync(string path, int requestedSize, CancellationToken cancellationToken = default)
    {
        return NativeIconHelper.GetThumbnailAsync(path, requestedSize, cancellationToken);
    }

    public BitmapSource? GetIconForFile(string filePath)
    {
        return NativeIconHelper.GetIconForFile(filePath);
    }
}
