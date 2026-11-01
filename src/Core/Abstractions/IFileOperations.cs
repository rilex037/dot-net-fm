namespace DotNetFM;

public interface IFileOperations
{
    readonly record struct OperationResult(bool Success, string? ErrorMessage = null);

    enum ConflictPolicy
    {
        Overwrite,
        Skip,
        Cancel
    }

    OperationResult Rename(FolderItem item, string newName);

    OperationResult DeleteToTrash(string fullPath, bool isFolder);

    bool HasNameConflicts(IReadOnlyList<string> sources, string targetDir);

    IReadOnlyList<OperationResult> TransferFiles(
        IReadOnlyList<string> sources, string targetDir, bool forceCopy,
        ConflictPolicy conflictPolicy = ConflictPolicy.Overwrite);

    OperationResult ExecuteFileWithArguments(string targetPath, IReadOnlyList<string> droppedFiles);
}
