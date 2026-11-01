using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

namespace DotNetFM;

public sealed record ArchiveProgress(string CurrentItem, int Completed, int Total, bool Indeterminate);

public static class ArchiveService
{
    public static bool HasExtractConflicts(string archivePath, string destinationDir, ArchiveEngine? engine)
    {
        try
        {
            if (engine != null)
                return Directory.Exists(destinationDir) && Directory.EnumerateFileSystemEntries(destinationDir).Any();

            string destRoot = Path.GetFullPath(destinationDir);

            if (IsPlainGzip(archivePath))
                return EntryTargetExists(destRoot, PlainGzipTargetName(archivePath));

            if (IsTarArchive(archivePath))
            {
                using var stream = OpenTarStream(archivePath);
                using var reader = new TarReader(stream);
                TarEntry? entry;
                while ((entry = reader.GetNextEntry()) != null)
                {
                    if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.Directory)) continue;
                    if (EntryTargetExists(destRoot, entry.Name)) return true;
                }
                return false;
            }

            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) continue;
                if (EntryTargetExists(destRoot, entry.FullName)) return true;
            }

            return false;
        }
        catch
        {
            try
            {
                return Directory.Exists(destinationDir) && Directory.EnumerateFileSystemEntries(destinationDir).Any();
            }
            catch
            {
                return false;
            }
        }
    }

    public static async Task ExtractAsync(string archivePath, string destinationDir, bool overwrite,
        ArchiveEngine? engine, IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        if (engine == null && !ArchiveFormats.IsBuiltin(archivePath))
            throw new NotSupportedException(
                $"“{Path.GetFileName(archivePath)}” needs an external archiver (7-Zip, NanaZip or WinRAR) or a built-in format (ZIP, TAR, GZIP).");

        Directory.CreateDirectory(destinationDir);

        if (engine != null)
        {
            string arguments = BuildExtractArguments(engine, archivePath, destinationDir, overwrite);
            await RunEngineAsync(engine, arguments, Path.GetDirectoryName(archivePath) ?? "", progress, token);
            return;
        }

        if (IsPlainGzip(archivePath))
        {
            await Task.Run(() => ExtractGzipCore(archivePath, destinationDir, overwrite, progress, token), token);
            return;
        }

        if (IsTarArchive(archivePath))
        {
            await Task.Run(() => ExtractTarCore(archivePath, destinationDir, overwrite, progress, token), token);
            return;
        }

        await Task.Run(() => ExtractZipCore(archivePath, destinationDir, overwrite, progress, token), token);
    }

    public static async Task CompressAsync(IReadOnlyList<string> sources, string archivePath, ArchiveFormat format,
        ArchiveEngine? engine, IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        if (format is ArchiveFormat.SevenZip or ArchiveFormat.Rar && engine == null)
            throw new NotSupportedException(
                $"{ArchiveService.GetArchiveExtension(format).TrimStart('.').ToUpperInvariant()} archives can only be created with an external archiver (7-Zip, NanaZip or WinRAR).");

        if (engine != null)
        {
            string workingDir = Path.GetDirectoryName(sources[0]) ?? "";
            string[] relativeNames = sources.Select(RelativeEntryName).ToArray();
            string arguments = BuildCompressArguments(engine, archivePath, relativeNames);
            await RunEngineAsync(engine, arguments, workingDir, progress, token);
            return;
        }

        if (format == ArchiveFormat.TarGz)
        {
            await Task.Run(() => CompressTarCore(sources, archivePath, progress, token), token);
            return;
        }

        await Task.Run(() => CompressZipCore(sources, archivePath, progress, token), token);
    }

    private static void ExtractZipCore(string archivePath, string destinationDir, bool overwrite,
        IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        string destRoot = Path.GetFullPath(destinationDir);

        using var archive = ZipFile.OpenRead(archivePath);
        int total = archive.Entries.Count;
        int completed = 0;

        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();

            string targetPath = GetSafeTargetPath(destRoot, entry.FullName);
            if (targetPath.Length > 0)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(targetPath);
                }
                else if (overwrite || !File.Exists(targetPath))
                {
                    string? parent = Path.GetDirectoryName(targetPath);
                    if (parent != null) Directory.CreateDirectory(parent);
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
            }

            completed++;
            progress.Report(new ArchiveProgress(entry.FullName, completed, total, false));
        }
    }

    private static void ExtractTarCore(string archivePath, string destinationDir, bool overwrite,
        IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        string destRoot = Path.GetFullPath(destinationDir);

        using var stream = OpenTarStream(archivePath);
        using var reader = new TarReader(stream);

        int completed = 0;
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) != null)
        {
            token.ThrowIfCancellationRequested();

            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.Directory)) continue;

            string targetPath = GetSafeTargetPath(destRoot, entry.Name);
            if (targetPath.Length > 0)
            {
                if (entry.EntryType == TarEntryType.Directory)
                {
                    Directory.CreateDirectory(targetPath);
                }
                else if (overwrite || !File.Exists(targetPath))
                {
                    string? parent = Path.GetDirectoryName(targetPath);
                    if (parent != null) Directory.CreateDirectory(parent);
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
            }

            completed++;
            progress.Report(new ArchiveProgress(entry.Name, completed, 0, false));
        }
    }

    private static void ExtractGzipCore(string archivePath, string destinationDir, bool overwrite,
        IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        string targetPath = Path.Combine(destinationDir, PlainGzipTargetName(archivePath));

        if (overwrite || !File.Exists(targetPath))
        {
            using var input = File.OpenRead(archivePath);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = File.Create(targetPath);
            CopyStream(gzip, output, token);
        }

        progress.Report(new ArchiveProgress(Path.GetFileName(targetPath), 1, 1, false));
    }

    private static void CompressZipCore(IReadOnlyList<string> sources, string archivePath,
        IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        using var stream = File.Create(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        int total = CountFiles(sources);
        int completed = 0;

        foreach (string source in sources)
        {
            token.ThrowIfCancellationRequested();

            if (File.Exists(source))
            {
                AddZipEntry(archive, source, Path.GetFileName(source), progress, ref completed, total, token);
            }
            else if (Directory.Exists(source))
            {
                string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
                archive.CreateEntry(folderName + "/");
                progress.Report(new ArchiveProgress(folderName + "/", ++completed, total, false));

                foreach (string file in EnumerateFilesSafe(source))
                {
                    token.ThrowIfCancellationRequested();
                    string relative = GetRelativeEntryPath(source, file);
                    AddZipEntry(archive, file, folderName + "/" + relative, progress, ref completed, total, token);
                }
            }
        }
    }

    private static void AddZipEntry(ZipArchive archive, string filePath, string entryName,
        IProgress<ArchiveProgress> progress, ref int completed, int total, CancellationToken token)
    {
        archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
        progress.Report(new ArchiveProgress(entryName, ++completed, total, false));
    }

    private static void CompressTarCore(IReadOnlyList<string> sources, string archivePath,
        IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        using var fileStream = File.Create(archivePath);
        using var gzip = new GZipStream(fileStream, CompressionLevel.Optimal);
        using var writer = new TarWriter(gzip, TarEntryFormat.Pax);

        int total = CountFiles(sources);
        int completed = 0;

        foreach (string source in sources)
        {
            token.ThrowIfCancellationRequested();

            if (File.Exists(source))
            {
                WriteTarEntry(writer, source, Path.GetFileName(source));
                progress.Report(new ArchiveProgress(Path.GetFileName(source), ++completed, total, false));
            }
            else if (Directory.Exists(source))
            {
                string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));

                foreach (string file in EnumerateFilesSafe(source))
                {
                    token.ThrowIfCancellationRequested();
                    string relative = GetRelativeEntryPath(source, file);
                    WriteTarEntry(writer, file, folderName + "/" + relative);
                    progress.Report(new ArchiveProgress(folderName + "/" + relative, ++completed, total, false));
                }
            }
        }
    }

    private static void WriteTarEntry(TarWriter writer, string filePath, string entryName)
    {
        var info = new FileInfo(filePath);
        using var data = File.OpenRead(filePath);
        var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
        {
            DataStream = data,
            ModificationTime = info.LastWriteTimeUtc
        };
        writer.WriteEntry(entry);
    }

    private static async Task RunEngineAsync(ArchiveEngine engine, string arguments, string workingDirectory,
        IProgress<ArchiveProgress> progress, CancellationToken token)
    {
        progress.Report(new ArchiveProgress($"Running {engine.DisplayName}…", 0, 0, true));

        var startInfo = new ProcessStartInfo
        {
            FileName = engine.ExecutablePath,
            Arguments = arguments,
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : "",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var registration = token.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); }
            catch { }
        });

        await Task.Run(() => process.WaitForExit(), CancellationToken.None);

        token.ThrowIfCancellationRequested();

        string errors = "";
        try { errors = (await stderrTask).Trim(); }
        catch { }

        try { await stdoutTask; }
        catch { }

        if (process.ExitCode != 0)
            throw new IOException($"{engine.DisplayName} failed with exit code {process.ExitCode}.\n{TruncateText(errors, 600)}");
    }

    private static string BuildExtractArguments(ArchiveEngine engine, string archivePath, string destinationDir, bool overwrite)
    {
        if (engine.Kind == ArchiveEngineKind.WinRar)
            return $"x -y {(overwrite ? "-o+" : "-o-")} -- \"{archivePath}\" \"{destinationDir.TrimEnd('\\')}\"";

        return $"x -y {(overwrite ? "-aoa" : "-aos")} -bd -o\"{destinationDir}\" -- \"{archivePath}\"";
    }

    private static string BuildCompressArguments(ArchiveEngine engine, string archivePath, string[] relativeNames)
    {
        string targets = string.Join(" ", relativeNames.Select(n => $"\"{n}\""));

        if (engine.Kind == ArchiveEngineKind.WinRar)
            return $"a -y -- \"{archivePath}\" {targets}";

        return $"a -t7z -mx=7 -y -bd -- \"{archivePath}\" {targets}";
    }

    public static string GetUniqueArchivePath(string directory, string fileName)
    {
        string candidate = Path.Combine(directory, fileName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;

        string ext = Path.GetExtension(fileName);
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        int counter = 2;

        while (true)
        {
            candidate = Path.Combine(directory, $"{baseName} ({counter}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            counter++;
        }
    }

    public static string GetArchiveExtension(ArchiveFormat format) => format switch
    {
        ArchiveFormat.SevenZip => ".7z",
        ArchiveFormat.TarGz => ".tar.gz",
        ArchiveFormat.Rar => ".rar",
        _ => ".zip"
    };

    private static string RelativeEntryName(string path) =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(path));

    private static string GetRelativeEntryPath(string root, string file)
    {
        string relative = Path.GetRelativePath(root, file);
        return relative.Replace('\\', '/');
    }

    private static int CountFiles(IReadOnlyList<string> sources)
    {
        int count = 0;
        foreach (string source in sources)
        {
            if (File.Exists(source)) count++;
            else if (Directory.Exists(source)) count += EnumerateFilesSafe(source).Count();
        }
        return count;
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string dir = pending.Pop();

            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch { subDirs = []; }

            foreach (string sub in subDirs)
            {
                try
                {
                    if (new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                }
                catch
                {
                    continue;
                }
                pending.Push(sub);
            }

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { files = []; }

            foreach (string file in files)
                yield return file;
        }
    }

    private static bool IsTarArchive(string path)
    {
        string name = Path.GetFileName(path);
        return name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlainGzip(string path)
    {
        string name = Path.GetFileName(path);
        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)) return false;
        return name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
    }

    private static Stream OpenTarStream(string archivePath)
    {
        var input = File.OpenRead(archivePath);
        if (archivePath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)) return input;
        return new GZipStream(input, CompressionMode.Decompress);
    }

    private static string PlainGzipTargetName(string archivePath)
    {
        string name = Path.GetFileName(archivePath);
        string stripped = name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name;
        return stripped.Length == 0 ? "extracted" : stripped;
    }

    private static bool EntryTargetExists(string destRoot, string? entryName)
    {
        if (string.IsNullOrEmpty(entryName)) return false;

        try
        {
            string combined = Path.Combine(destRoot, entryName);
            return File.Exists(combined) || Directory.Exists(combined);
        }
        catch
        {
            return false;
        }
    }

    private static string GetSafeTargetPath(string destRoot, string? entryName)
    {
        if (string.IsNullOrEmpty(entryName)) return "";

        try
        {
            string combined = Path.GetFullPath(Path.Combine(destRoot, entryName));
            string root = destRoot.EndsWith(Path.DirectorySeparatorChar)
                ? destRoot
                : destRoot + Path.DirectorySeparatorChar;
            return combined.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? combined : "";
        }
        catch
        {
            return "";
        }
    }

    private static void CopyStream(Stream source, Stream target, CancellationToken token)
    {
        var buffer = new byte[81920];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            target.Write(buffer, 0, read);
        }
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return "…" + text[^maxLength..];
    }
}
