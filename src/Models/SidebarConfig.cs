using System.Collections.Generic;

namespace dot_net_fm;

/// <summary>
/// Root configuration for sidebar items, icon mappings, and bookmarks.
/// Loaded from sidebar-config.json next to the executable.
/// </summary>
public class SidebarItemConfig
{
    public Dictionary<string, string> SidebarIcons { get; set; } = new();
    public List<SidebarSectionItem> Sections { get; set; } = new();
    public List<SidebarItem> Bookmarks { get; set; } = new();
}

public class SidebarSectionItem
{
    public string Name { get; set; } = "";
    public List<SidebarItem> Items { get; set; } = new();
}