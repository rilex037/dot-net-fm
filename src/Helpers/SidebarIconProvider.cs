namespace DotNetFM;

public static class SidebarIconProvider
{
    private static Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
    private const string DefaultIcon = "sidebar-home.svg";

    public static void Initialize(Dictionary<string, string> sidebarIcons)
    {
        _map = new Dictionary<string, string>(sidebarIcons, StringComparer.OrdinalIgnoreCase);
    }

    public static string GetIconPath(string itemName)
    {
        var file = _map.TryGetValue(itemName, out var mapped) ? mapped : DefaultIcon;
        return IconProvider.GetFullPath(file);
    }
}
