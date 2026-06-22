
namespace DotNetFM;

/// <summary>
/// Saves and restores open tab paths to/from the AppStore.
/// Keys: tabs.count, tabs.0, tabs.1, ..., tabs.active.
/// </summary>
public static class TabPersistenceService
{
    /// <summary>
    /// Saves the list of open tab paths and the active tab index to the store.
    /// </summary>
    public static void SaveTabs(IReadOnlyList<TabStore> tabs, TabStore? activeTab)
    {
        AppStore.Write("tabs.count", tabs.Count.ToString());

        for (int i = 0; i < tabs.Count; i++)
            AppStore.Write($"tabs.{i}", tabs[i].State.ActivePath);

        int activeIndex = 0;
        if (activeTab != null)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].State.TabId == activeTab.State.TabId)
                {
                    activeIndex = i;
                    break;
                }
            }
        }
        AppStore.Write("tabs.active", activeIndex.ToString());
    }

    /// <summary>
    /// Loads saved tab paths from the store.
    /// Returns an empty list if no tabs were saved (first run).
    /// </summary>
    public static List<string> LoadTabPaths()
    {
        string countStr = AppStore.Read("tabs.count");
        if (!int.TryParse(countStr, out int count) || count <= 0)
            return new List<string>();

        var paths = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            string path = AppStore.Read($"tabs.{i}");
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// Loads the saved active tab index from the store.
    /// Returns 0 if not found.
    /// </summary>
    public static int LoadActiveTabIndex()
    {
        string indexStr = AppStore.Read("tabs.active");
        return int.TryParse(indexStr, out int index) ? index : 0;
    }
}