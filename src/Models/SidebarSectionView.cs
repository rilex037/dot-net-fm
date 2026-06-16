using System.Collections.ObjectModel;

namespace DotNetFM;

/// <summary>
/// View model for a sidebar section rendered by SidebarPanel.
/// Each section has a title and a list of navigable items.
/// </summary>
public class SidebarSectionView
{
    public string Title { get; set; } = "";
    public ObservableCollection<SidebarItem.Item> Items { get; set; } = new();
}
