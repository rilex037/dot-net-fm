using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace dot_net_fm;

/// <summary>
/// Windows-specific IFileOperations implementation for local filesystem operations.
/// Handles rename, delete (to recycle bin), and transfer (copy/move) with Windows-specific behavior.
/// </summary>
public sealed class WindowsFileOperations : IFileOperations
{
    public IFileOperations.OperationResult Rename(FolderItem item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return new IFileOperations.OperationResult(false, "Name cannot be empty.");

        string? dir = Path.GetDirectoryName(item.FullPath);
        if (dir == null)
            return new IFileOperations.OperationResult(false, "Could not determine parent directory.");

        string newPath = Path.Combine(dir, newName.Trim());

        if (string.Equals(newPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
            return new IFileOperations.OperationResult(true);

        try
        {
            if (item.IsFolder)
            {
                if (!Directory.Exists(item.FullPath))
                    return new IFileOperations.OperationResult(false, "Source directory no longer exists.");
                if (Directory.Exists(newPath))
                    return new IFileOperations.OperationResult(false, "A directory with that name already exists.");

                Directory.Move(item.FullPath, newPath);
            }
            else
            {
                if (!File.Exists(item.FullPath))
                    return new IFileOperations.OperationResult(false, "Source file no longer exists.");
                if (File.Exists(newPath))
                    return new IFileOperations.OperationResult(false, "A file with that name already exists.");

                File.Move(item.FullPath, newPath);
            }

            item.Name = newName.Trim();
            item.FullPath = newPath;
            return new IFileOperations.OperationResult(true);
        }
        catch (Exception ex)
        {
            return new IFileOperations.OperationResult(false, $"Rename failed: {ex.Message}");
        }
    }

    public IFileOperations.OperationResult DeleteToTrash(string fullPath, bool isFolder)
    {
        try
        {
            if (isFolder)
            {
                FileSystem.DeleteDirectory(
                    fullPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
            }
            else
            {
                FileSystem.DeleteFile(
                    fullPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
            }

            return new IFileOperations.OperationResult(true);
        }
        catch (Exception ex)
        {
            return new IFileOperations.OperationResult(false, $"Delete failed: {ex.Message}");
        }
    }

    public IReadOnlyList<IFileOperations.OperationResult> TransferFiles(
        IReadOnlyList<string> sources, string targetDir, bool forceCopy)
    {
        var results = new List<IFileOperations.OperationResult>(sources.Count);

        foreach (var source in sources)
        {
            try
            {
                string name = Path.GetFileName(source);
                string dest = GetUniquePath(targetDir, name);
                bool sameDrive = string.Equals(
                    Path.GetPathRoot(source), Path.GetPathRoot(targetDir),
                    StringComparison.OrdinalIgnoreCase);

                if (File.Exists(source))
                {
                    if (!forceCopy && sameDrive)
                        File.Move(source, dest);
                    else
                        File.Copy(source, dest, overwrite: false);
                }
                else if (Directory.Exists(source))
                {
                    if (!forceCopy && sameDrive)
                        Directory.Move(source, dest);
                    else
                        FileSystem.CopyDirectory(source, dest);
                }
                else
                {
                    results.Add(new IFileOperations.OperationResult(false, $"'{name}' no longer exists."));
                    continue;
                }

                results.Add(new IFileOperations.OperationResult(true));
            }
            catch (Exception ex)
            {
                results.Add(new IFileOperations.OperationResult(false,
                    $"Transfer failed for '{Path.GetFileName(source)}': {ex.Message}"));
            }
        }

        return results;
    }

    private static string GetUniquePath(string dir, string name)
    {
        string dest = Path.Combine(dir, name);
        if (!File.Exists(dest) && !Directory.Exists(dest))
            return dest;

        string ext = Path.GetExtension(name);
        string baseName = Path.GetFileNameWithoutExtension(name);
        int counter = 1;
        while (true)
        {
            string candidate = Path.Combine(dir, $"{baseName} - {counter}{ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
            counter++;
        }
    }
}