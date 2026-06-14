namespace dot_net_fm;

/// <summary>
/// A bookmarked location stored with its ModuleUri so the app knows
/// which module to route to when the bookmark is clicked.
/// The address bar never shows the internal prefix.
/// </summary>
public sealed class Bookmark
{
    /// <summary>Display name shown in the sidebar (e.g., "Documents", "Server Files").</summary>
    public string Name { get; set; } = "";

    /// <summary>The module-qualified URI (e.g., "windows://C:\Users\Admin\Documents").</summary>
    public ModuleUri Location { get; set; }

    /// <summary>Icon name/key for the sidebar icon.</summary>
    public string Icon { get; set; } = "Folder";

    /// <summary>The human-readable path for display in the address bar.</summary>
    public string DisplayPath => Location.Path;
}