using System.Collections.Generic;

namespace FmDn;

/// <summary>
/// Root configuration for sidebar items, icon mappings, and bookmarks.
/// Loaded from sidebar-config.json next to the executable.
/// </summary>
public class SidebarConfig
{
    public Dictionary<string, string> SidebarIcons { get; set; } = new();
    public List<SidebarSection> Sections { get; set; } = new();
    public List<BookmarkEntry> Bookmarks { get; set; } = new();
}

public class SidebarSection
{
    public string Name { get; set; } = "";
    public List<SidebarEntry> Items { get; set; } = new();
}

public class SidebarEntry
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Path { get; set; } = "";
}

public class BookmarkEntry
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Path { get; set; } = "";
}