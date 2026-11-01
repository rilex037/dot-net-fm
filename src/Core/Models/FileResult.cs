namespace DotNetFM;

public sealed record FileResult
{
    public IReadOnlyList<FolderItem> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Offset { get; init; }

    public int Count => Items.Count;

    public bool HasMore => Offset + Count < TotalCount;

    public bool HasPrevious => Offset > 0;
}
