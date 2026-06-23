namespace DotNetFM;

/// <summary>
/// Provides file system operations (rename, delete, transfer) for a specific backend.
/// </summary>
public interface IFileOperations
{
    /// <summary>Result of a file system operation.</summary>
    readonly record struct OperationResult(bool Success, string? ErrorMessage = null);

    /// <summary>
    /// How to handle a destination item that already exists.
    /// Decided once per transfer batch — never per individual file/folder.
    /// </summary>
    enum ConflictPolicy
    {
        /// <summary>Replace existing files; merge into existing folders.</summary>
        Overwrite,
        /// <summary>Keep existing items untouched; only copy/move items that don't conflict.</summary>
        Skip,
        /// <summary>Abort the whole transfer as soon as a conflict is reached.</summary>
        Cancel
    }

    /// <summary>Renames a file or folder.</summary>
    OperationResult Rename(FolderItem item, string newName);

    /// <summary>Sends a file or folder to the recycle bin / trash.</summary>
    OperationResult DeleteToTrash(string fullPath, bool isFolder);

    /// <summary>
    /// True if transferring any of <paramref name="sources"/> into <paramref name="targetDir"/>
    /// would collide with something already there, so the caller knows whether to prompt at all.
    /// </summary>
    bool HasNameConflicts(IReadOnlyList<string> sources, string targetDir);

    /// <summary>Moves or copies files to the target directory.</summary>
    IReadOnlyList<OperationResult> TransferFiles(
        IReadOnlyList<string> sources, string targetDir, bool forceCopy,
        ConflictPolicy conflictPolicy = ConflictPolicy.Overwrite);

    /// <summary>
    /// Executes the file at <paramref name="targetPath"/> with the dropped files as arguments.
    /// For executables this launches the process; for non-executables the shell handles it
    /// (which may or may not support arguments). Returns <c>false</c> if the operation is
    /// unsupported by the module.
    /// </summary>
    OperationResult ExecuteFileWithArguments(string targetPath, IReadOnlyList<string> droppedFiles);
}
