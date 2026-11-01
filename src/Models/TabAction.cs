namespace DotNetFM;

public abstract record TabAction
{
    public sealed record NavigateTo(string Path, bool PushToHistory = true) : TabAction;

    public sealed record GoBack : TabAction;

    public sealed record GoForward : TabAction;

    public sealed record GoUp : TabAction;

    public sealed record SetIconSize(int Size) : TabAction;

    public sealed record SetSort(string SortKey, bool SortAscending) : TabAction;

    public sealed record BeginRefresh : TabAction;

    public sealed record DirectoryLoaded(
        string Path,
        IReadOnlyList<FolderItem> Items
    ) : TabAction;

    public sealed record TitleUpdated(string Title) : TabAction;

    public sealed record StatusTextUpdated(string Text) : TabAction;

    public sealed record NavStateUpdated(
        bool CanGoBack,
        bool CanGoForward,
        bool CanGoUp
    ) : TabAction;

    public sealed record DirectoryFileChangeDetected : TabAction;
}
