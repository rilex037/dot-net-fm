using System;
using System.Collections.Generic;
using System.IO;

namespace FmDn;

/// <summary>
/// Maps sidebar item names to SVG icon file paths.
/// Uses the SidebarIcons dictionary from sidebar-config.json.
/// </summary>
public static class SidebarIconMapper
{
    private static Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

    private const string DefaultIcon = "sidebar-home.svg";

    private static string BasePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons");

    /// <summary>
    /// Initializes the mapper with sidebar icon mappings.
    /// </summary>
    public static void Initialize(Dictionary<string, string> sidebarIcons)
    {
        _map = new Dictionary<string, string>(sidebarIcons, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetIconPath(string itemName)
    {
        var file = _map.TryGetValue(itemName, out var mapped) ? mapped : DefaultIcon;
        return Path.Combine(BasePath, file);
    }
}