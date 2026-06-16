namespace DotNetFM;

/// <summary>
/// Provides file system operations (rename, delete, transfer) for a specific backend.
/// </summary>
public interface IFileOperations
{
    /// <summary>Result of a file system operation.</summary>
    readonly record struct OperationResult(bool Success, string? ErrorMessage = null);

    /// <summary>Renames a file or folder.</summary>
    OperationResult Rename(FolderItem item, string newName);

    /// <summary>Sends a file or folder to the recycle bin / trash.</summary>
    OperationResult DeleteToTrash(string fullPath, bool isFolder);

    /// <summary>Moves or copies files to the target directory.</summary>
    IReadOnlyList<OperationResult> TransferFiles(
        IReadOnlyList<string> sources, string targetDir, bool forceCopy);
}
