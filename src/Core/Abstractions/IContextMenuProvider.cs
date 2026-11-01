using System.Windows;

namespace DotNetFM;

public interface IContextMenuProvider
{
    void Show(Window ownerWindow, Point screenPoint, IReadOnlyList<string> selectedPaths);

    bool ShowBackground(Window ownerWindow, Point screenPoint, string directoryPath);
}
