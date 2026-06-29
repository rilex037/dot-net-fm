namespace DotNetFM;

/// <summary>
/// Maps sidebar item names (e.g. "Home", "Desktop") to SVG filename lookups
/// via <see cref="IconProvider.GetFullPath"/>.
/// Initialized once from <c>sidebar-config.json</c> via <see cref="Initialize"/>.
/// </summary>
public static class SidebarIconProvider
{
    private static Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
    private const string DefaultIcon = "sidebar-home.svg";

    /// <summary>
    /// Initializes the provider with sidebar icon name→filename mappings.
    /// Called once at startup from <see cref="MainWindow.InitializeSidebar"/>.
    /// </summary>
    public static void Initialize(Dictionary<string, string> sidebarIcons)
    {
        _map = new Dictionary<string, string>(sidebarIcons, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a sidebar icon name to its full SVG file path.
    /// Falls back to <c>sidebar-home.svg</c> if the name isn't mapped.
    /// </summary>
    public static string GetIconPath(string itemName)
    {
        var file = _map.TryGetValue(itemName, out var mapped) ? mapped : DefaultIcon;
        return IconProvider.GetFullPath(file);
    }
}