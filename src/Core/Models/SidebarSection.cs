namespace DotNetFM;

public sealed class SidebarSection
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public List<SidebarEntry> Entries { get; set; } = [];

    public int Order { get; set; }
}

public sealed class SidebarEntry
{
    public string Name { get; set; } = "";

    public string Path { get; set; } = "";

    public string Icon { get; set; } = "Folder";
}
