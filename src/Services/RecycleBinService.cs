using System.IO;
using System.Security.Principal;
using System.Text;

namespace DotNetFM;

public static class RecycleBinService
{
    public const string RecycleBinPath = "::{645FF040-5081-101B-9F08-00AA002F954E}";

    private const string BinFolderName = "$Recycle.Bin";
    private const int FixedHeaderSize = 24;
    private const int Version2PathOffset = 28;

    public sealed record RecycleEntry(string Name, string Path);

    private sealed record BinPair(string DataPath, string MetadataPath);

    private sealed record MetaRecord(string MetadataPath, DateTime DeletedAt, string OriginalPath)
    {
        public string OriginalName => GetFileNamePart(OriginalPath);
    }

    public static bool IsRecycleBin(string? path) =>
        path != null && path.Equals(RecycleBinPath, StringComparison.OrdinalIgnoreCase);

    public static bool IsRecycleBinItemPath(string path) =>
        path.Contains("\\$Recycle.Bin\\", StringComparison.OrdinalIgnoreCase);

    public static List<string> Restore(IEnumerable<RecycleEntry> entries, List<string> restoredFolders, IEnumerable<string>? binDirectories = null)
    {
        var errors = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bins = binDirectories != null ? binDirectories.ToList() : GetBinDirectories().ToList();

        foreach (var entry in entries)
        {
            try
            {
                var pair = ResolvePair(entry.Name, entry.Path, used, bins);
                if (pair == null)
                {
                    errors.Add($"Could not find the recycle bin entry for \u201c{entry.Name}\u201d.");
                    continue;
                }

                string? original = ReadOriginalPath(pair.MetadataPath);
                if (string.IsNullOrWhiteSpace(original))
                {
                    errors.Add($"Could not read the original location of \u201c{entry.Name}\u201d.");
                    continue;
                }

                string? parent = GetParentDirectory(original);
                if (parent != null)
                    Directory.CreateDirectory(parent);

                string dest = original;
                if (File.Exists(dest) || Directory.Exists(dest))
                    dest = GetUniqueDestination(parent ?? Path.GetPathRoot(original) ?? @"C:\", GetFileNamePart(original));

                if (Directory.Exists(pair.DataPath))
                    Directory.Move(pair.DataPath, dest);
                else if (File.Exists(pair.DataPath))
                    File.Move(pair.DataPath, dest);
                else
                {
                    errors.Add($"The recycle bin data for \u201c{entry.Name}\u201d is missing.");
                    continue;
                }

                used.Add(pair.MetadataPath);

                try
                {
                    File.Delete(pair.MetadataPath);
                }
                catch (Exception ex)
                {
                    errors.Add($"Restored \u201c{entry.Name}\u201d, but could not remove its recycle bin record: {ex.Message}");
                }

                if (parent != null)
                    restoredFolders.Add(parent);
            }
            catch (Exception ex)
            {
                errors.Add($"Could not restore \u201c{entry.Name}\u201d: {ex.Message}");
            }
        }

        return errors;
    }

    public static List<string> DeletePermanently(IEnumerable<RecycleEntry> entries, IEnumerable<string>? binDirectories = null)
    {
        var errors = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bins = binDirectories != null ? binDirectories.ToList() : GetBinDirectories().ToList();

        foreach (var entry in entries)
        {
            try
            {
                var pair = ResolvePair(entry.Name, entry.Path, used, bins);
                if (pair == null)
                {
                    errors.Add($"Could not find the recycle bin entry for \u201c{entry.Name}\u201d.");
                    continue;
                }

                if (Directory.Exists(pair.DataPath))
                    Directory.Delete(pair.DataPath, recursive: true);
                else if (File.Exists(pair.DataPath))
                    File.Delete(pair.DataPath);

                File.Delete(pair.MetadataPath);
                used.Add(pair.MetadataPath);
            }
            catch (Exception ex)
            {
                errors.Add($"Could not delete \u201c{entry.Name}\u201d: {ex.Message}");
            }
        }

        return errors;
    }

    public static string GetBinSignature()
    {
        long count = 0;
        long totalBytes = 0;
        long newestTicks = 0;

        foreach (string dir in GetBinDirectories())
        {
            string[] metadataFiles;
            try
            {
                metadataFiles = Directory.GetFiles(dir, "$I*");
            }
            catch
            {
                continue;
            }

            foreach (string file in metadataFiles)
            {
                try
                {
                    var info = new FileInfo(file);
                    count++;
                    totalBytes += info.Length;
                    if (info.LastWriteTimeUtc.Ticks > newestTicks)
                        newestTicks = info.LastWriteTimeUtc.Ticks;
                }
                catch
                {
                }
            }
        }

        return $"{count}:{totalBytes}:{newestTicks}";
    }

    internal static string? GetCompanionName(string fileName)
    {
        if (fileName.Length < 3) return null;
        if (fileName.StartsWith("$R", StringComparison.OrdinalIgnoreCase))
            return "$I" + fileName[2..];
        if (fileName.StartsWith("$I", StringComparison.OrdinalIgnoreCase))
            return "$R" + fileName[2..];
        return null;
    }

    internal static string? ParseOriginalPath(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < FixedHeaderSize + 2) return null;

        int offset = bytes[0] >= 2 ? Version2PathOffset : FixedHeaderSize;
        if (offset >= bytes.Length) return null;

        int end = offset;
        while (end + 1 < bytes.Length && (bytes[end] != 0 || bytes[end + 1] != 0))
            end += 2;

        if (end + 1 >= bytes.Length) return null;

        string path = Encoding.Unicode.GetString(bytes, offset, end - offset);
        return path.Length == 0 ? null : path;
    }

    private static BinPair? ResolvePair(string name, string path, HashSet<string> used, IEnumerable<string> binDirectories)
    {
        if (IsRecycleBinItemPath(path))
        {
            string file = GetFileNamePart(path);
            string? dir = GetParentDirectory(path);
            if (dir != null)
            {
                var direct = BuildPair(dir, file);
                if (direct != null) return direct;
            }
        }

        string baseName = GetFileNamePart(path);
        if (baseName.StartsWith("$R", StringComparison.OrdinalIgnoreCase) ||
            baseName.StartsWith("$I", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string binDir in binDirectories)
            {
                var pair = BuildPair(binDir, baseName);
                if (pair != null) return pair;
            }
        }

        string? originalPath = AsFileSystemPath(path) ?? AsFileSystemPath(name);
        string matchName = GetFileNamePart(name);
        if (matchName.Length == 0) matchName = baseName;

        MetaRecord? best = null;
        int bestRank = 0;

        foreach (var record in EnumerateMetadata(binDirectories).OrderByDescending(r => r.DeletedAt))
        {
            if (used.Contains(record.MetadataPath)) continue;

            int rank = 0;
            if (originalPath != null && record.OriginalPath.Equals(originalPath, StringComparison.OrdinalIgnoreCase))
                rank = 2;
            else if (record.OriginalName.Equals(matchName, StringComparison.OrdinalIgnoreCase))
                rank = 1;

            if (rank <= bestRank) continue;

            string dataPath = DataPathFor(record.MetadataPath);
            if (rank == 1 && !File.Exists(dataPath) && !Directory.Exists(dataPath)) continue;

            best = record;
            bestRank = rank;
            if (bestRank == 2) break;
        }

        return best != null ? new BinPair(DataPathFor(best.MetadataPath), best.MetadataPath) : null;
    }

    private static string DataPathFor(string metadataPath) => Path.Combine(
        GetParentDirectory(metadataPath) ?? "",
        "$R" + GetFileNamePart(metadataPath)[2..]);

    private static string? AsFileSystemPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.StartsWith("::", StringComparison.Ordinal)) return null;
        if (!Path.IsPathRooted(value)) return null;
        return value;
    }

    private static BinPair? BuildPair(string dir, string fileName)
    {
        string? companion = GetCompanionName(fileName);
        if (companion == null) return null;

        string dataPath = Path.Combine(dir, fileName.StartsWith("$R", StringComparison.OrdinalIgnoreCase) ? fileName : companion);
        string metaPath = Path.Combine(dir, fileName.StartsWith("$I", StringComparison.OrdinalIgnoreCase) ? fileName : companion);

        if (File.Exists(dataPath) || Directory.Exists(dataPath) ||
            File.Exists(metaPath))
            return new BinPair(dataPath, metaPath);

        return null;
    }

    private static string? ReadOriginalPath(string metadataPath)
    {
        try
        {
            return ParseOriginalPath(File.ReadAllBytes(metadataPath));
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<MetaRecord> EnumerateMetadata(IEnumerable<string> binDirectories)
    {
        foreach (string binDir in binDirectories)
        {
            string[] metadataFiles;
            try
            {
                metadataFiles = Directory.GetFiles(binDir, "$I*");
            }
            catch
            {
                continue;
            }

            foreach (string metaPath in metadataFiles)
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(metaPath);
                }
                catch
                {
                    continue;
                }

                if (bytes.Length < FixedHeaderSize + 2) continue;

                string? original = ParseOriginalPath(bytes);
                if (string.IsNullOrWhiteSpace(original)) continue;

                long fileTime = BitConverter.ToInt64(bytes, 16);
                DateTime deletedAt = DateTime.FromFileTime(fileTime);

                yield return new MetaRecord(metaPath, deletedAt, original);
            }
        }
    }

    private static IEnumerable<string> GetBinDirectories()
    {
        string? sid;
        try
        {
            sid = WindowsIdentity.GetCurrent().User?.Value;
        }
        catch
        {
            sid = null;
        }

        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch
        {
            yield break;
        }

        var ownDirs = new List<string>();
        var otherDirs = new List<string>();

        foreach (var drive in drives)
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;

            string binRoot = Path.Combine(drive.RootDirectory.FullName, BinFolderName);
            if (!Directory.Exists(binRoot)) continue;

            string? own = null;
            if (sid != null)
            {
                own = Path.Combine(binRoot, sid);
                if (Directory.Exists(own))
                    ownDirs.Add(own);
            }

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(binRoot);
            }
            catch
            {
                continue;
            }

            foreach (string sub in subDirs)
            {
                if (own != null && sub.Equals(own, StringComparison.OrdinalIgnoreCase)) continue;
                if (GetFileNamePart(sub).StartsWith("S-1-", StringComparison.Ordinal))
                    otherDirs.Add(sub);
            }
        }

        foreach (string dir in ownDirs)
            yield return dir;

        foreach (string dir in otherDirs)
            yield return dir;
    }

    internal static string GetFileNamePart(string path)
    {
        int sep = path.LastIndexOfAny(['\\', '/']);
        return sep >= 0 ? path[(sep + 1)..] : path;
    }

    internal static string? GetParentDirectory(string path)
    {
        int sep = path.LastIndexOfAny(['\\', '/']);
        if (sep < 0) return null;

        string parent = path[..sep];
        return parent.Length == 0 ? null : parent;
    }

    private static string GetUniqueDestination(string dir, string name)
    {
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
