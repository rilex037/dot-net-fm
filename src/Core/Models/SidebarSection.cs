namespace DotNetFM;

/// <summary>
/// A section contributed by a module to the sidebar.
/// Each module can have one or more sidebar sections.
/// </summary>
public sealed class SidebarSection
{
    /// <summary>Stable identifier for persisting UI state (e.g., collapsed/expanded).</summary>
    public string Id { get; set; } = "";

    /// <summary>Section title displayed in the sidebar (e.g., "Local Files", "Network").</summary>
    public string Title { get; set; } = "";

    /// <summary>Display items in this section.</summary>
    public List<SidebarEntry> Entries { get; set; } = [];

    /// <summary>Priority order for displaying this section (lower = higher).</summary>
    public int Order { get; set; }
}

/// <summary>
/// A single entry in a sidebar section.
/// </summary>
public sealed class SidebarEntry
{
    /// <summary>Display name (e.g., "Home", "Desktop", "Documents").</summary>
    public string Name { get; set; } = "";

    /// <summary>The path or URI to navigate to. Can be a ModuleUri or plain path.</summary>
    public string Path { get; set; } = "";

    /// <summary>Icon key for the sidebar icon.</summary>
    public string Icon { get; set; } = "Folder";
}
