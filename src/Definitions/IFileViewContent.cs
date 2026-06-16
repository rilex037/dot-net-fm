using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Interface for child views hosted inside <see cref="FileViewContainer"/>.
/// Each view only provides its visual structure (ItemsControl for hit-testing, ScrollViewer for scrollbar check).
/// All interaction logic lives in the container.
/// </summary>
public interface IFileViewContent
{
    /// <summary>The ItemsControl used for hit-testing item positions.</summary>
    ItemsControl ItemsControl { get; }

    /// <summary>The ScrollViewer, used by the container to avoid rubber-banding near the scrollbar.</summary>
    ScrollViewer? ScrollViewer { get; }

    /// <summary>Raised when the view receives a mouse wheel event (for zoom forwarding).</summary>
    event Action<MouseWheelEventArgs>? MouseWheelPreview;
}
