using System.Windows;

namespace DotNetFM;

public sealed class WindowsContextMenuProvider : IContextMenuProvider
{
    public void Show(Window ownerWindow, Point screenPoint, IReadOnlyList<string> selectedPaths)
    {
        ShellContextMenuService.Show(ownerWindow, screenPoint, selectedPaths);
    }

    public bool ShowBackground(Window ownerWindow, Point screenPoint, string directoryPath)
    {
        return ShellContextMenuService.ShowBackground(ownerWindow, screenPoint, directoryPath);
    }
}
