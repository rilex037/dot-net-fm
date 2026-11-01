using System.Collections;
using System.Windows.Input;

namespace DotNetFM;

public interface IFileView
{
    IEnumerable? Folders { get; set; }
    FileInteractionService? InteractionService { get; set; }
    DragDropService? DragDropService { get; set; }
    string CurrentPath { get; set; }
    int IconSize { get; set; }

    void CommitAnyRename();
    void FocusRenameTextBox(FolderItem item);
    void ResetScroll(double offset = 0);
    double VerticalOffset { get; }

    event Action<MouseWheelEventArgs>? MouseWheelPreview;

    event Action<string>? OpenInNewTabRequested;
}
