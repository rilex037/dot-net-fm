namespace DotNetFM;

public interface IModule
{
    string ModuleId { get; }

    string DisplayName { get; }

    IReadOnlyList<string> UriPrefixes { get; }

    int Order { get; }

    IFileProvider FileProvider { get; }

    IFileOperations FileOperations { get; }

    IIconProvider IconProvider { get; }

    IContextMenuProvider? ContextMenuProvider { get; }

    IDirectoryWatcher CreateDirectoryWatcher();

    IReadOnlyList<SidebarSection> GetSidebarSections();

    string? ResolveDisplayName(string name);
}
