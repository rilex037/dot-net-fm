using System.IO;

namespace DotNetFM;

/// <summary>
///     Pure-function reducer that takes current TabStateRecord + TabAction
///     and returns the next state via with expressions.
///     Handles: navigation (NavigateTo, GoBack/Forward/Up), directory loaded,
///     refresh, icon size, internal sync (title, status, nav state), fallback.
/// </summary>
public static class TabReducer
{
    public static TabStateRecord Reduce(TabStateRecord state, TabAction action) => action switch
    {
        // ── Navigation ────────────────────────────────────────────

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

        // ── Directory content ─────────────────────────────────────

        TabAction.DirectoryLoaded a => state with
        {
            ActivePath  = a.Path,
            Title       = DeriveTitle(a.Path),
            LoadStatus  = TabLoadStatus.Idle,
            ItemCount   = a.Items.Count,
        },

        // ── Refresh ───────────────────────────────────────────────

        TabAction.BeginRefresh => state with
        {
            LoadStatus = TabLoadStatus.Refreshing
        },

        TabAction.DirectoryFileChangeDetected => state with
        {
            LoadStatus = TabLoadStatus.Refreshing
        },

        // ── UI state ─────────────────────────────────────────────

        TabAction.SetIconSize a => state with
        {
            IconSize = a.Size
        },

        // ── Internal state updates from NavigationService ────────

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

        // ── Fallback ─────────────────────────────────────────────

        _ => state
    };

    private static string DeriveTitle(string path)
    {
        string name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
