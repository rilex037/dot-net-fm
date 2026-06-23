using System.IO;

namespace DotNetFM;

/// <summary>
/// Maps icon names to SVG file paths under Assets/Icons.
/// Also provides static resolution for any icon filename.
/// </summary>
public static class IconProvider
{
    private static Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
    private const string DefaultIcon = "sidebar-home.svg";

    private static string BasePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons");

    /// <summary>
    /// Initializes the provider with sidebar icon mappings.
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
        return Path.Combine(BasePath, file);
    }

    /// <summary>
    /// Returns the full path to any icon file under Assets/Icons.
    /// </summary>
    public static string GetFullPath(string iconFileName) => Path.Combine(BasePath, iconFileName);
}