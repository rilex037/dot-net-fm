namespace DotNetFM;

public sealed class Bookmark
{
    public string Name { get; set; } = "";

    public ModuleUri Location { get; set; } = new("", "");

    public string Icon { get; set; } = "Folder";

    public string DisplayPath => Location.Path;
}
