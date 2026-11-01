using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace DotNetFM;

public sealed class WindowsFileOperations : IFileOperations
{
    private const uint SHGFI_EXETYPE = 0x2000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, IntPtr psfi, uint cbFileInfo, uint uFlags);

    public IFileOperations.OperationResult Rename(FolderItem item, string newName)
    {
        string trimmed = newName.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return new IFileOperations.OperationResult(false, "Name cannot be empty.");

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return new IFileOperations.OperationResult(false, "The name contains characters that are not allowed.");

        string? dir = Path.GetDirectoryName(item.FullPath);
        if (dir == null)
            return new IFileOperations.OperationResult(false, "Could not determine parent directory.");

        string newPath = Path.Combine(dir, trimmed);

        if (string.Equals(newPath, item.FullPath, StringComparison.Ordinal))
            return new IFileOperations.OperationResult(true);

        bool caseOnlyRename = string.Equals(newPath, item.FullPath, StringComparison.OrdinalIgnoreCase);

        try
        {
            if (item.IsFolder)
            {
                if (!Directory.Exists(item.FullPath))
                    return new IFileOperations.OperationResult(false, "Source directory no longer exists.");
                if (!caseOnlyRename && Directory.Exists(newPath))
                    return new IFileOperations.OperationResult(false, "A directory with that name already exists.");

                Directory.Move(item.FullPath, newPath);
            }
            else
            {
                if (!File.Exists(item.FullPath))
                    return new IFileOperations.OperationResult(false, "Source file no longer exists.");
                if (!caseOnlyRename && File.Exists(newPath))
                    return new IFileOperations.OperationResult(false, "A file with that name already exists.");

                File.Move(item.FullPath, newPath);
            }

            item.Name = trimmed;
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

    public bool HasNameConflicts(IReadOnlyList<string> sources, string targetDir)
    {
        string fullTargetDir = Path.GetFullPath(targetDir);

        foreach (var source in sources)
        {
            string? sourceDir = Path.GetDirectoryName(source);
            if (sourceDir != null &&
                string.Equals(Path.GetFullPath(sourceDir), fullTargetDir, StringComparison.OrdinalIgnoreCase))
                continue;

            string dest = Path.Combine(targetDir, Path.GetFileName(source));
            if (File.Exists(dest) || Directory.Exists(dest))
                return true;
        }

        return false;
    }

    public IReadOnlyList<IFileOperations.OperationResult> TransferFiles(
        IReadOnlyList<string> sources, string targetDir, bool forceCopy,
        IFileOperations.ConflictPolicy conflictPolicy = IFileOperations.ConflictPolicy.Overwrite)
    {
        var results = new List<IFileOperations.OperationResult>(sources.Count);
        string fullTargetDir = Path.GetFullPath(targetDir);

        foreach (var source in sources)
        {
            try
            {
                string name = Path.GetFileName(source);
                bool isMove = !forceCopy && string.Equals(
                    Path.GetPathRoot(source), Path.GetPathRoot(targetDir),
                    StringComparison.OrdinalIgnoreCase);

                string? sourceDir = Path.GetDirectoryName(source);
                bool isDuplicateInPlace = sourceDir != null &&
                    string.Equals(Path.GetFullPath(sourceDir), fullTargetDir, StringComparison.OrdinalIgnoreCase);

                if (Directory.Exists(source))
                {
                    string fullSource = Path.GetFullPath(source).TrimEnd('\\');
                    string fullTarget = fullTargetDir.TrimEnd('\\');
                    if (string.Equals(fullSource, fullTarget, StringComparison.OrdinalIgnoreCase) ||
                        fullTarget.StartsWith(fullSource + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new IFileOperations.OperationResult(false,
                            $"Cannot move \u201c{name}\u201d into itself."));
                        continue;
                    }
                }

                string dest = Path.Combine(targetDir, name);
                bool destExists = File.Exists(dest) || Directory.Exists(dest);

                if (isDuplicateInPlace)
                {
                    if (!forceCopy)
                    {
                        results.Add(new IFileOperations.OperationResult(true));
                        continue;
                    }
                    if (destExists)
                    {
                        dest = GetUniquePath(targetDir, name);
                        destExists = false;
                    }
                }
                else if (destExists && conflictPolicy == IFileOperations.ConflictPolicy.Cancel)
                {
                    break;
                }
                else if (destExists && conflictPolicy == IFileOperations.ConflictPolicy.Skip)
                {
                    results.Add(new IFileOperations.OperationResult(true));
                    continue;
                }

                if (File.Exists(source))
                {
                    if (isMove && !destExists)
                        File.Move(source, dest);
                    else if (isMove)
                    {
                        File.Copy(source, dest, overwrite: true);
                        File.Delete(source);
                    }
                    else
                        File.Copy(source, dest, overwrite: true);
                }
                else if (Directory.Exists(source))
                {
                    if (!destExists)
                    {
                        if (isMove)
                            Directory.Move(source, dest);
                        else
                            FileSystem.CopyDirectory(source, dest);
                    }
                    else
                    {
                        bool fullyMerged = MergeDirectories(source, dest, conflictPolicy);
                        if (isMove && fullyMerged)
                            Directory.Delete(source, recursive: true);
                    }
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

    private static bool MergeDirectories(string source, string dest, IFileOperations.ConflictPolicy conflictPolicy)
    {
        Directory.CreateDirectory(dest);
        bool fullyMerged = true;

        foreach (var subDir in Directory.GetDirectories(source))
        {
            string destSubDir = Path.Combine(dest, Path.GetFileName(subDir));
            if (!MergeDirectories(subDir, destSubDir, conflictPolicy))
                fullyMerged = false;
        }

        foreach (var file in Directory.GetFiles(source))
        {
            string destFile = Path.Combine(dest, Path.GetFileName(file));
            if (File.Exists(destFile) && conflictPolicy == IFileOperations.ConflictPolicy.Skip)
            {
                fullyMerged = false;
                continue;
            }

            File.Copy(file, destFile, overwrite: true);
        }

        return fullyMerged;
    }

    public IFileOperations.OperationResult ExecuteFileWithArguments(string targetPath, IReadOnlyList<string> droppedFiles)
    {
        try
        {
            if (SHGetFileInfo(targetPath, 0, IntPtr.Zero, 0, SHGFI_EXETYPE) == IntPtr.Zero)
                return new IFileOperations.OperationResult(true);

            string args = string.Join(" ", droppedFiles.Select(f => $"\"{f}\""));
            var startInfo = new ProcessStartInfo(targetPath)
            {
                UseShellExecute = true,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(targetPath))
            };

            Process.Start(startInfo);
            return new IFileOperations.OperationResult(true);
        }
        catch (Exception ex)
        {
            return new IFileOperations.OperationResult(false,
                $"Could not execute '{Path.GetFileName(targetPath)}' with dropped files: {ex.Message}");
        }
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
