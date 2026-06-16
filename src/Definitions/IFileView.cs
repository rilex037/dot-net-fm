using System;
using System.Collections;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Abstraction for any file view (grid, list, etc.).
/// Decouples the host window from specific view implementations.
/// </summary>
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

    /// <summary>Raised when the user middle-clicks a folder item to open it in a new tab.</summary>
    event Action<string>? OpenInNewTabRequested;
}