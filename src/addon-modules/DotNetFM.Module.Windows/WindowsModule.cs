using System.IO;
using System.Linq;

namespace DotNetFM;

/// <summary>
/// Windows file system module. Provides local filesystem browsing,
/// native Windows shell context menus, Win32 icon loading, and
/// Windows-specific sidebar entries (drives, system folders).
/// </summary>
public sealed class WindowsModule : IModule
{
    private readonly WindowsIconProvider _iconProvider;
    private readonly WindowsFileProvider _fileProvider;
    private readonly WindowsFileOperations _fileOperations;
    private readonly WindowsContextMenuProvider? _contextMenuProvider;

    public string ModuleId => "windows";
    public string DisplayName => "Local Files";
    public int Order => 0;

    public IReadOnlyList<string> UriPrefixes => _prefixes.Value;

    public IFileProvider FileProvider => _fileProvider;
    public IFileOperations FileOperations => _fileOperations;
    public IIconProvider IconProvider => _iconProvider;
    public IContextMenuProvider? ContextMenuProvider => _contextMenuProvider;

    // ── Known CLSIDs for shell namespace virtual folders ──────────
    public static readonly Dictionary<string, string> KnownClsids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["::mycomputer"] = "My Computer",
        ["::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"] = "This PC",
        ["::{645FF040-5081-101B-9F08-00AA002F954E}"] = "Recycle Bin",
        ["::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}"] = "Network",
        ["::{D20EA4E1-3957-11D2-848B-00C04FD43608}"] = "Control Panel",
    };

    // ── Static prefixes + dynamic drive letter prefixes ───────────
    private static readonly string[] _staticPrefixes = ["windows", "shell", "::{"];

    private static string[] CollectAllPrefixes()
    {
        string[] drives;
        try
        {
            drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.Name.TrimEnd('\\'))  // "C:\", "D:\" → "C:", "D:"
                .ToArray();
        }
        catch
        {
            drives = [];
        }

        return [.._staticPrefixes, ..drives];
    }

    private readonly Lazy<string[]> _prefixes = new(CollectAllPrefixes);

    public WindowsModule()
    {
        _iconProvider = new WindowsIconProvider();
        _fileProvider = new WindowsFileProvider();
        _fileOperations = new WindowsFileOperations();
        _contextMenuProvider = new WindowsContextMenuProvider();
    }

    public IDirectoryWatcher CreateDirectoryWatcher() => new WindowsDirectoryWatcher();

    /// <summary>
    /// Resolves a user-typed display name back to the internal path.
    /// Used when navigating via the address bar with human-readable names.
    /// </summary>
    public string? ResolveDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Reverse lookup: "Recycle Bin" → "::{645FF040-...}", "My Computer" → "::mycomputer"
        foreach (var (path, displayName) in KnownClsids)
        {
            if (displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    public IReadOnlyList<SidebarSection> GetSidebarSections()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return
        [
            new SidebarSection
            {
                Id = "mycomputer",
                Title = "My Computer",
                Order = 0,
                Entries =
                [
                    new SidebarEntry { Name = Environment.UserName, Path = userProfile, Icon = "Home" },
                    new SidebarEntry { Name = "Desktop", Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Icon = "Desktop" },
                    new SidebarEntry { Name = "Documents", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Icon = "Documents" },
                    new SidebarEntry { Name = "Downloads", Path = Path.Combine(userProfile, "Downloads"), Icon = "Downloads" },
                    new SidebarEntry { Name = "Pictures", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Icon = "Pictures" },
                    new SidebarEntry { Name = "Music", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Icon = "Music" },
                    new SidebarEntry { Name = "Videos", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Icon = "Videos" },
                    new SidebarEntry { Name = "File System", Path = WindowsFileProvider.MyComputerPath, Icon = "MyComputer" },
                ]
            }
        ];
    }
}
