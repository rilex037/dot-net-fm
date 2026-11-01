using System.IO;
using System.Windows;

namespace DotNetFM;

public sealed class FileInteractionService(
    ClickTracker? clickTracker = null,
    RenameManager? renameManager = null,
    Func<string, IFileOperations?>? resolveFileOperations = null,
    ProcessLaunchService? processLauncher = null,
    ClipboardService? clipboard = null,
    Func<string, IFileProvider?>? resolveFileProvider = null)
{
    private readonly ClickTracker _clickTracker = clickTracker ?? new();
    private readonly RenameManager _renameManager = renameManager ?? new();
    private readonly Func<string, IFileOperations?> _resolveFileOperations = resolveFileOperations ?? (_ => null);
    private readonly ProcessLaunchService _processLauncher = processLauncher ?? new();
    private readonly ClipboardService _clipboard = clipboard ?? new();
    private readonly Func<string, IFileProvider?> _resolveFileProvider = resolveFileProvider ?? (_ => null);

    public Action<string>? NavigateRequested;
    public Action<Point, List<string>>? ContextMenuRequested;
    public Action<string>? ErrorDisplayRequested;
    public Func<IFileOperations.ConflictPolicy>? ConflictResolutionRequested;
    public Action<IReadOnlyList<string>, string>? TransferCompleted;

    public ClickTracker ClickTracker => _clickTracker;
    public RenameManager RenameManager => _renameManager;
    public ProcessLaunchService ProcessLauncher => _processLauncher;
    public ClipboardService Clipboard => _clipboard;

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

    public bool CommitPendingRename(Action<FolderItem>? onCommitted)
    {
        return _renameManager.CommitActive(item =>
        {
            onCommitted?.Invoke(item);
        });
    }

    public void FinalizeRename(FolderItem item, string newName)
    {
        _renameManager.ClearActive();

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

    public void BeginRename(FolderItem item) => _renameManager.BeginRename(item);

    public void HandleRenameKey(FolderItem item, string newName, System.Windows.Input.Key key)
    {
        if (key == System.Windows.Input.Key.Enter)
            FinalizeRename(item, newName);
        else if (key == System.Windows.Input.Key.Escape)
            _renameManager.ClearActive();
    }

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

    public void DeletePathsToTrash(IReadOnlyList<string> paths)
    {
        var errors = new List<string>();

        foreach (var path in paths)
        {
            var ops = _resolveFileOperations(path);
            if (ops == null)
            {
                errors.Add($"No file operations available for '{path}'.");
                continue;
            }

            bool isFolder = Directory.Exists(path) && !File.Exists(path);
            var result = ops.DeleteToTrash(path, isFolder);
            if (!result.Success && result.ErrorMessage != null)
                errors.Add(result.ErrorMessage);
        }

        if (errors.Count > 0)
            ErrorDisplayRequested?.Invoke(string.Join("\n", errors));
    }

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

        var provider = _resolveFileProvider(currentDirectory);
        if (provider?.IsVirtualRoot(currentDirectory) == true) return;

        var paths = _clipboard.TryGetFileDropList();
        if (paths == null || paths.Count == 0) return;

        bool wasCut = _clipboard.IsCut;
        bool allSucceeded = RunTransfer(paths, currentDirectory, forceCopy: !wasCut);

        if (wasCut && allSucceeded)
            _clipboard.Clear();

        TransferCompleted?.Invoke(paths, currentDirectory);
    }

    public void HandleDroppedFiles(string[] sources, string targetDir, bool forceCopy)
    {
        RunTransfer(sources, targetDir, forceCopy);
        TransferCompleted?.Invoke(sources, targetDir);
    }

    public void HandleDropOnFile(string targetPath, List<string> droppedFiles)
    {
        var ops = _resolveFileOperations(targetPath);
        if (ops == null)
        {
            ErrorDisplayRequested?.Invoke("No file operations available for this location.");
            return;
        }

        var result = ops.ExecuteFileWithArguments(targetPath, droppedFiles);
        if (!result.Success && result.ErrorMessage != null)
            ErrorDisplayRequested?.Invoke(result.ErrorMessage);
    }

    private bool RunTransfer(IReadOnlyList<string> sources, string targetDir, bool forceCopy)
    {
        var ops = _resolveFileOperations(targetDir);
        if (ops == null)
        {
            ErrorDisplayRequested?.Invoke("No file operations available for this location.");
            return false;
        }

        var policy = IFileOperations.ConflictPolicy.Overwrite;
        if (ops.HasNameConflicts(sources, targetDir))
            policy = ConflictResolutionRequested?.Invoke() ?? IFileOperations.ConflictPolicy.Overwrite;

        if (policy == IFileOperations.ConflictPolicy.Cancel)
            return false;

        var results = ops.TransferFiles(sources, targetDir, forceCopy, policy);

        var errors = new List<string>();
        bool allSucceeded = true;
        foreach (var result in results)
        {
            if (result.Success) continue;
            allSucceeded = false;
            if (result.ErrorMessage != null)
                errors.Add(result.ErrorMessage);
        }

        if (errors.Count > 0)
            ErrorDisplayRequested?.Invoke(string.Join("\n", errors));

        return allSucceeded;
    }
}
