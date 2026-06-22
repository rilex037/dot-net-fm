using System.Windows;

namespace DotNetFM;

/// <summary>
/// Coordinates file interaction logic by composing pure, testable services.
/// No WPF UI types (ItemsControl, TextBox, Canvas, Border) are accepted —
/// all visual-tree work is the caller's responsibility.
/// Handles: click → rename/open dispatch, clipboard operations, delete, file transfer.
/// <para>
/// Holds no filesystem implementation of its own. Every operation that actually touches disk
/// (rename, delete-to-trash, transfer) is resolved per-path through <see cref="_resolveFileOperations"/>
/// to the owning module's <see cref="IFileOperations"/> — the same module that provides the
/// FileProvider/IconProvider/ContextMenuProvider for that path. This keeps platform-specific
/// behavior (e.g. the Windows module's recycle-bin delete) out of this cross-platform layer
/// entirely, instead of duplicating it here.
/// </para>
/// </summary>
public sealed class FileInteractionService(
    ClickTracker? clickTracker = null,
    RenameManager? renameManager = null,
    Func<string, IFileOperations?>? resolveFileOperations = null,
    ProcessLaunchService? processLauncher = null,
    ClipboardService? clipboard = null)
{
    private readonly ClickTracker _clickTracker = clickTracker ?? new();
    private readonly RenameManager _renameManager = renameManager ?? new();
    private readonly Func<string, IFileOperations?> _resolveFileOperations = resolveFileOperations ?? (_ => null);
    private readonly ProcessLaunchService _processLauncher = processLauncher ?? new();
    private readonly ClipboardService _clipboard = clipboard ?? new();

    // ── Events (set by MainWindow) ────────────────────────────────

    public Action<string>? NavigateRequested;
    public Action<Point, List<string>>? ContextMenuRequested;
    /// <summary>Called when the UI should display an error message.</summary>
    public Action<string>? ErrorDisplayRequested;
    /// <summary>
    /// Called at most once per transfer, only when something at the destination would actually
    /// be overwritten. The UI shows a single confirmation (not one per file/folder) and returns
    /// how to proceed for the whole batch.
    /// </summary>
    public Func<IFileOperations.ConflictPolicy>? ConflictResolutionRequested;

    // ── Public access to composed services ────────────────────────

    public ClickTracker ClickTracker => _clickTracker;
    public RenameManager RenameManager => _renameManager;
    public ProcessLaunchService ProcessLauncher => _processLauncher;
    public ClipboardService Clipboard => _clipboard;

    // ── Click / Selection dispatch ────────────────────────────────

    /// <summary>
    /// Handles a mouse-down on an item (icon or name).
    /// Returns true if the caller should handle the event as handled.
    /// </summary>
    public bool HandleItemMouseDown(FolderItem clickedItem, bool isNameClick,
        Action<IEnumerable<FolderItem>> clearAllSelections, bool allowRename = true)
    {
        if (clickedItem.IsEditing) return false;

        CommitPendingRename(null);

        var action = _clickTracker.RecordClick(clickedItem);

        switch (action)
        {
            case ClickTracker.ClickAction.Open:
                OpenItem(clickedItem);
                return true;

            case ClickTracker.ClickAction.BeginRename:
                if (!allowRename)
                    goto case ClickTracker.ClickAction.Select;
                if (isNameClick)
                    _renameManager.StartPending(clickedItem);
                return true;

            case ClickTracker.ClickAction.Select:
                _renameManager.CancelPending();
                clearAllSelections([]);
                clickedItem.IsSelected = true;
                return true;
        }

        return true;
    }

    /// <summary>
    /// Commits any active rename, passing the active item to the callback
    /// which the caller should use to find the TextBox and invoke <see cref="FinalizeRename"/>.
    /// </summary>
    public bool CommitPendingRename(Action<FolderItem>? onCommitted)
    {
        return _renameManager.CommitActive(item =>
        {
            onCommitted?.Invoke(item);
        });
    }

    /// <summary>Finalizes a rename using the text from an already-known new name.</summary>
    public void FinalizeRename(FolderItem item, string newName)
    {
        var ops = _resolveFileOperations(item.FullPath);
        if (ops == null)
        {
            ErrorDisplayRequested?.Invoke("No file operations available for this location.");
            return;
        }

        var result = ops.Rename(item, newName);
        if (!result.Success && result.ErrorMessage != null)
            ErrorDisplayRequested?.Invoke(result.ErrorMessage);
    }

    // ── Open ──────────────────────────────────────────────────────

    public void OpenItem(FolderItem item)
    {
        if (item.IsFolder)
        {
            NavigateRequested?.Invoke(item.FullPath);
            return;
        }

        var result = _processLauncher.OpenWithShell(item.FullPath);
        if (!result.Success && result.ErrorMessage != null)
            ErrorDisplayRequested?.Invoke(result.ErrorMessage);
    }

    // ── Rename ────────────────────────────────────────────────────

    /// <summary>Begins rename mode on the given item.</summary>
    public void BeginRename(FolderItem item) => _renameManager.BeginRename(item);

    /// <summary>Handles Enter (commit) / Escape (cancel) key presses during rename.</summary>
    public void HandleRenameKey(FolderItem item, string newName, System.Windows.Input.Key key)
    {
        if (key == System.Windows.Input.Key.Enter)
            FinalizeRename(item, newName);
        else if (key == System.Windows.Input.Key.Escape)
            item.IsEditing = false;
    }

    // ── Delete ────────────────────────────────────────────────────

    public void DeleteToTrash(FolderItem item)
    {
        var ops = _resolveFileOperations(item.FullPath);
        if (ops == null)
        {
            ErrorDisplayRequested?.Invoke("No file operations available for this location.");
            return;
        }

        var result = ops.DeleteToTrash(item.FullPath, item.IsFolder);
        if (!result.Success && result.ErrorMessage != null)
            ErrorDisplayRequested?.Invoke(result.ErrorMessage);
    }

    // ── Clipboard ─────────────────────────────────────────────────

    public void HandleCopy(IEnumerable<FolderItem> folders)
    {
        var paths = VisualTreeUtility.GetSelectedPaths(folders);
        _clipboard.Copy(paths);
    }

    public void HandleCut(IEnumerable<FolderItem> folders)
    {
        var paths = VisualTreeUtility.GetSelectedPaths(folders);
        _clipboard.Cut(paths);
    }

    public void HandlePaste(string currentDirectory)
    {
        if (string.IsNullOrEmpty(currentDirectory)) return;

        var paths = _clipboard.TryGetFileDropList();
        if (paths == null || paths.Count == 0) return;

        bool forceCopy = !_clipboard.IsCut;
        _clipboard.ResetCutFlag();

        RunTransfer(paths, currentDirectory, forceCopy);
    }

    /// <summary>
    /// Handles a drag-and-drop transfer. Resolves at most one conflict confirmation for the
    /// whole batch (via <see cref="ConflictResolutionRequested"/>) and surfaces any per-item
    /// failures through <see cref="ErrorDisplayRequested"/>.
    /// </summary>
    public void HandleDroppedFiles(string[] sources, string targetDir, bool forceCopy)
    {
        RunTransfer(sources, targetDir, forceCopy);
    }

    private void RunTransfer(IReadOnlyList<string> sources, string targetDir, bool forceCopy)
    {
        // The destination owns the write, so its module's IFileOperations does the transfer —
        // this is also what lets a future non-Windows module merge/overwrite its own way.
        var ops = _resolveFileOperations(targetDir);
        if (ops == null)
        {
            ErrorDisplayRequested?.Invoke("No file operations available for this location.");
            return;
        }

        var policy = IFileOperations.ConflictPolicy.Overwrite;
        if (ops.HasNameConflicts(sources, targetDir))
            policy = ConflictResolutionRequested?.Invoke() ?? IFileOperations.ConflictPolicy.Overwrite;

        if (policy == IFileOperations.ConflictPolicy.Cancel)
            return;

        var results = ops.TransferFiles(sources, targetDir, forceCopy, policy);

        foreach (var result in results)
        {
            if (!result.Success && result.ErrorMessage != null)
                ErrorDisplayRequested?.Invoke(result.ErrorMessage);
        }
    }
}
