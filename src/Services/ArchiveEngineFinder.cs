using System.IO;

namespace DotNetFM;

public enum ArchiveEngineKind
{
    SevenZipFamily,
    WinRar
}

public sealed record ArchiveEngine(string Id, string DisplayName, string ExecutablePath, ArchiveEngineKind Kind);

public static class ArchiveEngineFinder
{
    public static List<ArchiveEngine> Detect()
    {
        var engines = new List<ArchiveEngine>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string id, string displayName, string path, ArchiveEngineKind kind)
        {
            if (seenIds.Contains(id)) return;

            try
            {
                path = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            if (!File.Exists(path) || !seenPaths.Add(path)) return;

            engines.Add(new ArchiveEngine(id, displayName, path, kind));
            seenIds.Add(id);
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        TryAdd("7zip", "7-Zip", Path.Combine(programFiles, "7-Zip", "7z.exe"), ArchiveEngineKind.SevenZipFamily);
        TryAdd("7zip", "7-Zip", Path.Combine(programFilesX86, "7-Zip", "7z.exe"), ArchiveEngineKind.SevenZipFamily);

        TryAdd("nanazip", "NanaZip",
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "NanaZipC.exe"),
            ArchiveEngineKind.SevenZipFamily);
        TryAdd("nanazip", "NanaZip",
            Path.Combine(localAppData, "Microsoft", "WindowsApps", "7z.exe"),
            ArchiveEngineKind.SevenZipFamily);

        TryAdd("winrar", "WinRAR", Path.Combine(programFiles, "WinRAR", "WinRAR.exe"), ArchiveEngineKind.WinRar);
        TryAdd("winrar", "WinRAR", Path.Combine(programFilesX86, "WinRAR", "WinRAR.exe"), ArchiveEngineKind.WinRar);

        foreach (string found in FindOnPath(["7z.exe", "NanaZipC.exe", "WinRAR.exe"]))
        {
            string fileName = Path.GetFileNameWithoutExtension(found);
            if (fileName.Equals("7z", StringComparison.OrdinalIgnoreCase))
                TryAdd("7zip", "7-Zip", found, ArchiveEngineKind.SevenZipFamily);
            else if (fileName.Equals("NanaZipC", StringComparison.OrdinalIgnoreCase))
                TryAdd("nanazip", "NanaZip", found, ArchiveEngineKind.SevenZipFamily);
            else
                TryAdd("winrar", "WinRAR", found, ArchiveEngineKind.WinRar);
        }

        return engines;
    }

    public static ArchiveEngine? ResolvePreferred(List<ArchiveEngine>? detected = null)
    {
        var engines = detected ?? Detect();
        if (engines.Count == 0) return null;

        string setting = AppStore.Read("archives.engine");
        if (setting.Equals("builtin", StringComparison.OrdinalIgnoreCase)) return null;

        foreach (var engine in engines)
        {
            if (engine.Id.Equals(setting, StringComparison.OrdinalIgnoreCase)) return engine;
        }

        return engines[0];
    }

    public static ArchiveEngine? ResolveForFormat(ArchiveFormat format)
    {
        if (format is not (ArchiveFormat.SevenZip or ArchiveFormat.Rar)) return null;

        var engines = Detect();
        foreach (var engine in engines)
        {
            if (format == ArchiveFormat.SevenZip && engine.Kind == ArchiveEngineKind.SevenZipFamily) return engine;
            if (format == ArchiveFormat.Rar && engine.Kind == ArchiveEngineKind.WinRar) return engine;
        }

        return null;
    }

    private static IEnumerable<string> FindOnPath(string[] fileNames)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) yield break;

        foreach (string dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (dir.Length == 0) continue;

            foreach (string fileName in fileNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(dir, fileName);
                }
                catch
                {
                    continue;
                }

                if (File.Exists(candidate)) yield return candidate;
            }
        }
    }
}
