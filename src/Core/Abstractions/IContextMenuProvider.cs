using System.Windows;

namespace DotNetFM;

/// <summary>
/// Provides context menu functionality for a specific backend.
/// </summary>
public interface IContextMenuProvider
{
    /// <summary>
    /// Shows a context menu for the selected file paths.
    /// </summary>
    void Show(Window ownerWindow, Point screenPoint, IReadOnlyList<string> selectedPaths);
}
