using System.Windows;

namespace DotNetFM;

public interface IAppContextMenuHost
{
    void OpenPaths(IReadOnlyList<string> paths);
    void OpenPathInNewTab(string path);
    void CutPaths(IReadOnlyList<string> paths);
    void CopyPaths(IReadOnlyList<string> paths);
    void PasteInto(string targetDirectory);
    void RenameSelection();
    void DeletePaths(IReadOnlyList<string> paths);
    void RestoreFromRecycleBin(IReadOnlyList<string> paths);
    void DeleteFromRecycleBin(IReadOnlyList<string> paths);
    void CopyPathsAsText(IReadOnlyList<string> paths);
    void CreateNewFolder(string targetDirectory);
    void CreateNewTextDocument(string targetDirectory);
    void OpenTerminal(string targetDirectory);
    void ShowProperties(string path);
    void RefreshView();
    void SelectAllItems();
    void ToggleHiddenFiles();
    void ShowShellMenuForPaths(Point screenPoint, IReadOnlyList<string> paths);
    void ShowShellMenuForDirectory(Point screenPoint, string directory);
    void ExtractArchive(string archivePath, ArchiveExtractMode mode);
    void CompressItems(IReadOnlyList<string> paths, string? formatId);
    void ApplySort(string sortKey, bool ascending);
    string CurrentSortKey { get; }
    bool CurrentSortAscending { get; }
    bool HiddenFilesShown { get; }
    bool CanPaste();
}
