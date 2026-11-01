using System.IO;
using System.Windows;

namespace DotNetFM;

public static class ArchiveUi
{
    public static IReadOnlyList<(string Id, string Label)> AvailableCompressFormats()
    {
        var formats = new List<(string Id, string Label)>
        {
            ("zip", "ZIP"),
            ("targz", "TAR.GZ")
        };

        var engines = ArchiveEngineFinder.Detect();
        if (engines.Any(e => e.Kind == ArchiveEngineKind.SevenZipFamily))
            formats.Insert(1, ("7z", "7Z"));
        if (engines.Any(e => e.Kind == ArchiveEngineKind.WinRar))
            formats.Add(("rar", "RAR"));

        return formats;
    }

    public static string ResolveDefaultFormatId()
    {
        string setting = AppStore.Read("archives.format");
        if (setting.Length == 0) setting = "zip";

        foreach (var (id, _) in AvailableCompressFormats())
        {
            if (id.Equals(setting, StringComparison.OrdinalIgnoreCase)) return id;
        }

        return "zip";
    }

    public static string SuggestArchiveBaseName(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1)
        {
            string path = paths[0];
            if (Directory.Exists(path) && !File.Exists(path))
                return Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            return Path.GetFileNameWithoutExtension(path);
        }

        return "Archive";
    }

    public static void ExtractArchive(Window owner, string archivePath, ArchiveExtractMode mode,
        Action<IEnumerable<string>>? refreshFolders)
    {
        string archiveDir = Path.GetDirectoryName(archivePath) ?? "";
        string destination = mode switch
        {
            ArchiveExtractMode.Here => archiveDir,
            ArchiveExtractMode.Subfolder => Path.Combine(archiveDir, ArchiveFormats.GetBaseName(archivePath)),
            _ => ""
        };

        if (mode == ArchiveExtractMode.PickFolder)
        {
            destination = FolderPicker.Show(owner, "Select the folder to extract to", archiveDir) ?? "";
            if (destination.Length == 0) return;
        }

        var engine = ArchiveEngineFinder.ResolvePreferred();
        if (engine == null && !ArchiveFormats.IsBuiltin(archivePath))
        {
            CustomDialog.Show(
                "Extract Archive",
                $"“{Path.GetFileName(archivePath)}” cannot be extracted by the built-in engine, which handles ZIP, TAR and GZIP.\n\n" +
                "Install 7-Zip, NanaZip or WinRAR and DotNetFM will detect and use it automatically (no setup needed).",
                DialogIcon.Error);
            return;
        }

        bool overwrite = false;
        if (ArchiveService.HasExtractConflicts(archivePath, destination, engine))
        {
            var choice = CustomDialog.Show(
                "Confirm Replace",
                $"The destination already contains items with matching names:\n\n{destination}\n\n" +
                "Yes = overwrite them, No = keep the existing files, Cancel = stop.",
                DialogIcon.Confirmation,
                DialogButtons.YesNoCancel);

            if (choice == CustomDialogResult.Cancel) return;
            overwrite = choice == CustomDialogResult.Yes;
        }

        string title = $"Extracting {Path.GetFileName(archivePath)}";
        var outcome = ArchiveProgressDialog.Run(owner, title, (progress, token) =>
            ArchiveService.ExtractAsync(archivePath, destination, overwrite, engine, progress, token));

        HandleOutcome(owner, outcome, "Extract Archive", [destination, archiveDir], refreshFolders);
    }

    public static void Compress(Window owner, IReadOnlyList<string> sources, string? formatId,
        Action<IEnumerable<string>>? refreshFolders)
    {
        if (sources.Count == 0) return;

        string directory = Path.GetDirectoryName(sources[0]) ?? "";
        if (directory.Length == 0 || !Directory.Exists(directory)) return;

        var formats = AvailableCompressFormats();
        string defaultFormat = ResolveDefaultFormatId();

        string baseName = SuggestArchiveBaseName(sources);
        string chosenFormat = formatId ?? "";

        if (chosenFormat.Length == 0)
        {
            var picked = CompressDialog.Show(owner, baseName, formats, defaultFormat);
            if (picked == null) return;
            baseName = picked.Value.Name;
            chosenFormat = picked.Value.FormatId;
        }

        foreach (var (id, _) in formats)
        {
            if (id.Equals(chosenFormat, StringComparison.OrdinalIgnoreCase))
            {
                chosenFormat = id;
                break;
            }
        }

        ArchiveFormat format = ParseFormatId(chosenFormat);
        string archivePath = ArchiveService.GetUniqueArchivePath(directory,
            baseName + ArchiveService.GetArchiveExtension(format));

        var engine = ArchiveEngineFinder.ResolveForFormat(format);
        if (format is ArchiveFormat.SevenZip or ArchiveFormat.Rar && engine == null)
        {
            CustomDialog.Show(
                "Compress to Archive",
                $"{ArchiveService.GetArchiveExtension(format).TrimStart('.').ToUpperInvariant()} creation needs an external archiver (7-Zip, NanaZip or WinRAR).\n\n" +
                "ZIP and TAR.GZ always work with the built-in engine.",
                DialogIcon.Error);
            return;
        }

        string title = $"Compressing to {Path.GetFileName(archivePath)}";
        var outcome = ArchiveProgressDialog.Run(owner, title, (progress, token) =>
            ArchiveService.CompressAsync(sources, archivePath, format, engine, progress, token));

        if (outcome.Cancelled && File.Exists(archivePath))
        {
            try { File.Delete(archivePath); }
            catch { }
        }

        HandleOutcome(owner, outcome, "Compress to Archive", [directory], refreshFolders);
    }

    public static ArchiveFormat ParseFormatId(string id) => id.ToLowerInvariant() switch
    {
        "7z" => ArchiveFormat.SevenZip,
        "targz" => ArchiveFormat.TarGz,
        "rar" => ArchiveFormat.Rar,
        _ => ArchiveFormat.Zip
    };

    private static void HandleOutcome(Window owner, ArchiveOutcome outcome, string errorTitle,
        string[] folders, Action<IEnumerable<string>>? refreshFolders)
    {
        if (!outcome.Succeeded && !outcome.Cancelled && outcome.ErrorMessage != null)
        {
            CustomDialog.Show(errorTitle, outcome.ErrorMessage, DialogIcon.Error);
            return;
        }

        refreshFolders?.Invoke(folders);
    }
}
