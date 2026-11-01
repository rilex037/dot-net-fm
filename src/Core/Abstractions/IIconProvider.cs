using System.Windows.Media.Imaging;

namespace DotNetFM;

public interface IIconProvider
{
    Task<BitmapSource?> GetThumbnailAsync(string path, int requestedSize, CancellationToken cancellationToken = default);

    BitmapSource? GetIconForFile(string filePath);
}
