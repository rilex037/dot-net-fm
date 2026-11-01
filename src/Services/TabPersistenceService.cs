namespace DotNetFM;

public static class TabPersistenceService
{
    private static readonly HashSet<string> ValidSortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "size", "type", "date"
    };

    public sealed record SavedTab(string Path, string SortKey, bool SortAscending);

    public static void SaveTabs(IReadOnlyList<TabStore> tabs, TabStore? activeTab)
    {
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

        int previousCount = AppStore.ReadInt("tabs.count", 0);

        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tabs.count"] = tabs.Count.ToString(),
            ["tabs.active"] = activeIndex.ToString()
        };

        for (int i = 0; i < tabs.Count; i++)
        {
            changes[$"tabs.{i}"] = tabs[i].State.ActivePath;
            changes[$"tabs.{i}.sort"] = tabs[i].State.SortKey;
            changes[$"tabs.{i}.sortasc"] = tabs[i].State.SortAscending ? "1" : "0";
        }

        for (int i = tabs.Count; i < previousCount; i++)
        {
            changes[$"tabs.{i}"] = "";
            changes[$"tabs.{i}.sort"] = "";
            changes[$"tabs.{i}.sortasc"] = "";
        }

        AppStore.WriteMany(changes);
    }

    public static List<SavedTab> LoadTabs()
    {
        int count = AppStore.ReadInt("tabs.count", 0);
        if (count <= 0) return [];

        var tabs = new List<SavedTab>(count);
        for (int i = 0; i < count; i++)
        {
            string path = AppStore.Read($"tabs.{i}");
            if (string.IsNullOrEmpty(path)) continue;

            string sortKey = AppStore.Read($"tabs.{i}.sort");
            if (!ValidSortKeys.Contains(sortKey)) sortKey = "name";

            tabs.Add(new SavedTab(path, sortKey, AppStore.ReadBool($"tabs.{i}.sortasc", true)));
        }

        return tabs;
    }

    public static int LoadActiveTabIndex()
    {
        return AppStore.ReadInt("tabs.active", 0);
    }
}
