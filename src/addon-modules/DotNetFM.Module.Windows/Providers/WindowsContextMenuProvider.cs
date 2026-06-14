using System.Windows;

namespace dot_net_fm;

/// <summary>
/// Windows-specific IContextMenuProvider that shows the native Windows Shell context menu
/// for files and folders.
/// </summary>
public sealed class WindowsContextMenuProvider : IContextMenuProvider
{
    public void Show(Window ownerWindow, Point screenPoint, IReadOnlyList<string> selectedPaths)
    {
        ShellContextMenuService.Show(ownerWindow, screenPoint, selectedPaths);
    }
}