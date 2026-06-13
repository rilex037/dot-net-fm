using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace dot_net_fm;

/// <summary>
/// Pure file-system operations: rename, delete (to recycle bin), transfer (copy/move).
/// No UI dependencies. All methods return success/failure results instead of showing dialogs.
/// </summary>
public sealed class FileOperationService
{
    /// <summary>Result of a file system operation.</summary>
    public readonly record struct OperationResult(bool Success, string? ErrorMessage = null);

    /// <summary>
    /// Renames a file or folder. Returns failure if source doesn't exist or target already exists.
    /// On success, updates <paramref name="item"/>'s Name and FullPath properties.
    /// </summary>
    public OperationResult Rename(FolderItem item, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return new OperationResult(false, "Name cannot be empty.");

        string? dir = Path.GetDirectoryName(item.FullPath);
        if (dir == null)
            return new OperationResult(false, "Could not determine parent directory.");

        string newPath = Path.Combine(dir, newName.Trim());

        if (string.Equals(newPath, item.FullPath, StringComparison.OrdinalIgnoreCase))
            return new OperationResult(true);

        try
        {
            if (item.IsFolder)
            {
                if (!Directory.Exists(item.FullPath))
                    return new OperationResult(false, "Source directory no longer exists.");
                if (Directory.Exists(newPath))
                    return new OperationResult(false, "A directory with that name already exists.");

                Directory.Move(item.FullPath, newPath);
            }
            else
            {
                if (!File.Exists(item.FullPath))
                    return new OperationResult(false, "Source file no longer exists.");
                if (File.Exists(newPath))
                    return new OperationResult(false, "A file with that name already exists.");

                File.Move(item.FullPath, newPath);
            }

            item.Name = newName.Trim();
            item.FullPath = newPath;
            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Rename failed: {ex.Message}");
        }
    }

    /// <summary>Sends the item at <paramref name="fullPath"/> to the Recycle Bin.</summary>
    public OperationResult DeleteToTrash(string fullPath, bool isFolder)
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

            return new OperationResult(true);
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Delete failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves or copies files/directories to the target directory.
    /// Same-drive is moved by default; cross-drive is copied.
    /// Set <paramref name="forceCopy"/> to always copy (used by paste-after-copy).
    /// Duplicate names get a numeric suffix ("file - 1", "file - 2").
    /// Returns a list of results, one per source entry.
    /// </summary>
    public IReadOnlyList<OperationResult> TransferFiles(
        IReadOnlyList<string> sources, string targetDir, bool forceCopy)
    {
        var results = new List<OperationResult>(sources.Count);

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
                    results.Add(new OperationResult(false, $"'{name}' no longer exists."));
                    continue;
                }

                results.Add(new OperationResult(true));
            }
            catch (Exception ex)
            {
                results.Add(new OperationResult(false,
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