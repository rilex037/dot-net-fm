namespace DotNetFM;

public static class TabReducer
{
    public static TabStateRecord Reduce(TabStateRecord state, TabAction action) => action switch
    {
        TabAction.NavigateTo a => state with
        {
            ActivePath  = a.Path,
            Title       = DeriveTitle(a.Path),
            LoadStatus  = TabLoadStatus.Loading,
        },

        TabAction.GoBack or TabAction.GoForward or TabAction.GoUp => state with
        {
            LoadStatus = TabLoadStatus.Loading
        },

        TabAction.DirectoryLoaded a => state with
        {
            ActivePath  = a.Path,
            Title       = DeriveTitle(a.Path),
            LoadStatus  = TabLoadStatus.Idle,
            ItemCount   = a.Items.Count,
        },

        TabAction.BeginRefresh => state with
        {
            LoadStatus = TabLoadStatus.Refreshing
        },

        TabAction.DirectoryFileChangeDetected => state with
        {
            LoadStatus = TabLoadStatus.Refreshing
        },

        TabAction.SetIconSize a => state with
        {
            IconSize = a.Size
        },

        TabAction.SetSort a => state with
        {
            SortKey       = a.SortKey,
            SortAscending = a.SortAscending
        },

        TabAction.TitleUpdated a => state with
        {
            Title = a.Title
        },

        TabAction.StatusTextUpdated a => state with
        {
            StatusText = a.Text
        },

        TabAction.NavStateUpdated a => state with
        {
            CanGoBack    = a.CanGoBack,
            CanGoForward = a.CanGoForward,
            CanGoUp      = a.CanGoUp
        },

        _ => state
    };

    private static string DeriveTitle(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
