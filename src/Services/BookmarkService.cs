namespace DotNetFM;

public sealed record BookmarkEntry(string Name, string Path);

public static class BookmarkService
{
    public static List<BookmarkEntry> Load()
    {
        int count = AppStore.ReadInt("bookmarks.count", 0);
        var bookmarks = new List<BookmarkEntry>(count);

        for (int i = 0; i < count; i++)
        {
            string name = AppStore.Read($"bookmarks.{i}.name");
            string path = AppStore.Read($"bookmarks.{i}.path");
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path))
                bookmarks.Add(new BookmarkEntry(name, path));
        }

        return bookmarks;
    }

    public static void Save(IReadOnlyList<BookmarkEntry> bookmarks)
    {
        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bookmarks.count"] = bookmarks.Count.ToString()
        };

        for (int i = 0; i < bookmarks.Count; i++)
        {
            changes[$"bookmarks.{i}.name"] = bookmarks[i].Name;
            changes[$"bookmarks.{i}.path"] = bookmarks[i].Path;
        }

        AppStore.WriteMany(changes);
    }

    public static string SuggestName(string path)
    {
        string name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
