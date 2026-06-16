using System;
using System.Collections.Generic;
using System.Windows;

namespace DotNetFM;

/// <summary>
/// Coordinates file interaction logic by composing pure, testable services.
/// No WPF UI types (ItemsControl, TextBox, Canvas, Border) are accepted —
/// all visual-tree work is the caller's responsibility.
/// Handles: click → rename/open dispatch, clipboard operations, delete, file transfer.
/// </summary>
public sealed class FileInteractionService
{
    private readonly ClickTracker _clickTracker;
    private readonly RenameManager _renameManager;
    private readonly FileOperationService _fileOps;
    private readonly ProcessLaunchService _processLauncher;
    private readonly ClipboardService _clipboard;

    // ── Events (set by MainWindow) ────────────────────────────────

    public Action<string>? NavigateRequested;
    public Action<Point, List<string>>? ContextMenuRequested;
    /// <summary>Called when the UI should display an error message.</summary>
    public Action<string>? ErrorDisplayRequested;

    // ── Construction ───────────────────────────────────────────────

    public FileInteractionService(
        ClickTracker? clickTracker = null,
        RenameManager? renameManager = null,
        FileOperationService? fileOps = null,
        ProcessLaunchService? processLauncher = null,
        ClipboardService? clipboard = null)
    {
        _clickTracker = clickTracker ?? new ClickTracker();
        _renameManager = renameManager ?? new RenameManager();
        _fileOps = fileOps ?? new FileOperationService();
        _processLauncher = processLauncher ?? new ProcessLaunchService();
        _clipboard = clipboard ?? new ClipboardService();
    }

    // ── Public access to composed services ────────────────────────

    public ClickTracker ClickTracker => _clickTracker;
    public RenameManager RenameManager => _renameManager;
    public FileOperationService FileOperations => _fileOps;
    public ProcessLaunchService ProcessLauncher => _processLauncher;
    public ClipboardService Clipboard => _clipboard;

    // ── Click / Selection dispatch ────────────────────────────────

    /// <summary>
    /// Handles a mouse-down on an item (icon or name).
    /// Returns true if the caller should handle the event as handled.
    /// </summary>
    public bool HandleItemMouseDown(FolderItem clickedItem, bool isNameClick, Action<IEnumerable<FolderItem>> clearAllSelections)
    {
        if (clickedItem.IsEditing) return false;

        CommitPendingRename(clearAllSelections, null);

        var action = _clickTracker.RecordClick(clickedItem);

        switch (action)
        {
            case ClickTracker.ClickAction.Open:
                OpenItem(clickedItem);
                return true;

            case ClickTracker.ClickAction.BeginRename:
                if (isNameClick)
                    _renameManager.StartPending(clickedItem);
                return true;

            case ClickTracker.ClickAction.Select:
                _renameManager.CancelPending();
                clearAllSelections(Array.Empty<FolderItem>());
                clickedItem.IsSelected = true;
                return true;
        }

        return true;
    }

    /// <summary>
    /// Commits any active rename, passing the active item to the callback
    /// which the caller should use to find the TextBox and invoke <see cref="FinalizeRename"/>.
    /// </summary>
    public bool CommitPendingRename(Action<IEnumerable<FolderItem>> clearAllSelections, Action<FolderItem>? onCommitted)
    {
        return _renameManager.CommitActive(item =>
        {
            onCommitted?.Invoke(item);
        });
    }

    /// <summary>Finalizes a rename using the text from an already-known new name.</summary>
    public void FinalizeRename(FolderItem item, string newName)
    {
        var result = _fileOps.Rename(item, newName);
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
        var result = _fileOps.DeleteToTrash(item.FullPath, item.IsFolder);
        if (!result.Success && result.ErrorMessage != null)
            ErrorDisplayRequested?.Invoke(result.ErrorMessage);
    }

    // ── Clipboard ─────────────────────────────────────────────────

    public void HandleCopy(IEnumerable<FolderItem> folders)
    {
        var paths = GetSelectedPaths(folders);
        _clipboard.Copy(paths);
    }

    public void HandleCut(IEnumerable<FolderItem> folders)
    {
        var paths = GetSelectedPaths(folders);
        _clipboard.Cut(paths);
    }

    public void HandlePaste(string currentDirectory)
    {
        if (string.IsNullOrEmpty(currentDirectory)) return;

        var paths = _clipboard.TryGetFileDropList();
        if (paths == null || paths.Count == 0) return;

        var results = _fileOps.TransferFiles(paths, currentDirectory, forceCopy: !_clipboard.IsCut);
        _clipboard.ResetCutFlag();

        foreach (var result in results)
        {
            if (!result.Success && result.ErrorMessage != null)
                ErrorDisplayRequested?.Invoke(result.ErrorMessage);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static List<string> GetSelectedPaths(IEnumerable<FolderItem> folders)
    {
        var paths = new List<string>();
        foreach (var item in folders)
            if (item.IsSelected)
                paths.Add(item.FullPath);
        return paths;
    }
}
