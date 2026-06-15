using System.IO;

namespace dot_net_fm;

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
    public string UriPrefix => "windows";
    public int Order => 0;

    public IFileProvider FileProvider => _fileProvider;
    public IFileOperations FileOperations => _fileOperations;
    public IIconProvider IconProvider => _iconProvider;
    public IContextMenuProvider? ContextMenuProvider => _contextMenuProvider;

    public WindowsModule()
    {
        _iconProvider = new WindowsIconProvider();
        _fileProvider = new WindowsFileProvider();
        _fileOperations = new WindowsFileOperations();
        _contextMenuProvider = new WindowsContextMenuProvider();
    }

    public IDirectoryWatcher CreateDirectoryWatcher() => new WindowsDirectoryWatcher();

    public bool CanHandle(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        // Can handle: local paths (C:\, D:\, etc.) and "My Computer"
        if (path == WindowsFileProvider.MyComputerPath) return true;
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':') return true;

        return false;
    }

    public IReadOnlyList<SidebarSection> GetSidebarSections()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return
        [
            new SidebarSection
            {
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