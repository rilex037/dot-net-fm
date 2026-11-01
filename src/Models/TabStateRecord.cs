namespace DotNetFM;

public sealed record TabStateRecord
{
    public Guid TabId { get; init; } = Guid.NewGuid();

    public string ActivePath { get; init; } = "";

    public string Title { get; init; } = "New Tab";

    public string DisplayPath { get; init; } = "";

    public TabLoadStatus LoadStatus { get; init; } = TabLoadStatus.Idle;

    public int IconSize { get; init; } = AppStore.ReadInt("tab.iconsize", 64);

    public string SortKey { get; init; } = "name";

    public bool SortAscending { get; init; } = true;

    public bool CanGoBack { get; init; }

    public bool CanGoForward { get; init; }

    public bool CanGoUp { get; init; }

    public int ItemCount { get; init; }

    public string StatusText { get; init; } = "";
}
