
namespace DotNetFM;

/// <summary>
/// Discriminated union of all actions that can mutate a tab's state.
/// Dispatched through <see cref="TabStore.Dispatch"/>; the reducer produces
/// a new <see cref="TabStateRecord"/> from the current state + action.
/// </summary>
public abstract record TabAction
{
    // ── User-facing navigation actions ──────────────────────────────

    /// <summary>Navigate to a directory path.</summary>
    public sealed record NavigateTo(string Path, bool PushToHistory = true) : TabAction;

    /// <summary>Go back in the navigation history.</summary>
    public sealed record GoBack : TabAction;

    /// <summary>Go forward in the navigation history.</summary>
    public sealed record GoForward : TabAction;

    /// <summary>Navigate to the parent directory.</summary>
    public sealed record GoUp : TabAction;

    // ── User-facing UI actions ─────────────────────────────────────

    /// <summary>Change the icon zoom level.</summary>
    public sealed record SetIconSize(int Size) : TabAction;

    /// <summary>Re-load the current directory.</summary>
    public sealed record BeginRefresh : TabAction;

    // ── Internal actions (dispatched by the store) ─────────────────

    /// <summary>The directory content has been loaded from disk.</summary>
    public sealed record DirectoryLoaded(
        string Path,
        IReadOnlyList<FolderItem> Items
    ) : TabAction;

    /// <summary>NavigationService reports a new display name.</summary>
    public sealed record TitleUpdated(string Title) : TabAction;

    /// <summary>NavigationService reports a new status bar text.</summary>
    public sealed record StatusTextUpdated(string Text) : TabAction;

    /// <summary>NavigationService back/forward/up flags changed.</summary>
    public sealed record NavStateUpdated(
        bool CanGoBack,
        bool CanGoForward,
        bool CanGoUp
    ) : TabAction;

    /// <summary>The file-system watcher detected a change in the current directory.</summary>
    public sealed record DirectoryFileChangeDetected : TabAction;
}
