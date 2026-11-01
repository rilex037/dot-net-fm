using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

public interface IFileViewContent
{
    ItemsControl ItemsControl { get; }

    ScrollViewer? ScrollViewer { get; }

    event Action<MouseWheelEventArgs>? MouseWheelPreview;
}
