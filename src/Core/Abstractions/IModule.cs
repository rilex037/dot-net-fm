namespace dot_net_fm;

/// <summary>
/// A module provides file browsing capabilities for a specific backend
/// (local filesystem, FTP, cloud storage, etc.). Each module registers
/// a unique URI prefix used for bookmarks and internal routing.
/// Modules are auto-discovered by scanning assemblies for implementations.
/// </summary>
public interface IModule
{
    /// <summary>Unique identifier for this module (e.g., "windows", "ftp", "onedrive").</summary>
    string ModuleId { get; }

    /// <summary>Display name shown in the UI (e.g., "Local Files", "FTP Server").</summary>
    string DisplayName { get; }

    /// <summary>URI scheme prefixes (e.g., ["windows", "shell"], ["ftp"]). Used for routing and bookmark lookup.</summary>
    IReadOnlyList<string> UriPrefixes { get; }

    /// <summary>Priority order for sidebar sections (lower = higher priority).</summary>
    int Order { get; }

    /// <summary>The file provider for browsing files.</summary>
    IFileProvider FileProvider { get; }

    /// <summary>The file operations provider.</summary>
    IFileOperations FileOperations { get; }

    /// <summary>The icon provider for file thumbnails.</summary>
    IIconProvider IconProvider { get; }

    /// <summary>The context menu provider, or null if not supported.</summary>
    IContextMenuProvider? ContextMenuProvider { get; }

    /// <summary>Creates a new directory watcher instance for a tab.</summary>
    IDirectoryWatcher CreateDirectoryWatcher();

    /// <summary>Sidebar sections contributed by this module.</summary>
    IReadOnlyList<SidebarSection> GetSidebarSections();

    /// <summary>
    /// Resolves a user-typed display name back to the internal path.
    /// Returns null if the name doesn't match any known location.
    /// </summary>
    string? ResolveDisplayName(string name);
}
