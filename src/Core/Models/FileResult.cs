namespace DotNetFM;

/// <summary>
/// Paginated result from IFileProvider.GetItemsAsync.
/// Supports lazy loading: the client requests a specific range and can
/// calculate subsequent pages based on TotalCount, Offset, and Count.
/// </summary>
public sealed record FileResult
{
    /// <summary>The items in this page.</summary>
    public IReadOnlyList<FolderItem> Items { get; init; } = [];

    /// <summary>Total number of items in the directory.</summary>
    public int TotalCount { get; init; }

    /// <summary>Zero-based offset of the first item in this page.</summary>
    public int Offset { get; init; }

    /// <summary>Number of items returned in this page.</summary>
    public int Count => Items.Count;

    /// <summary>Whether there are more items after this page.</summary>
    public bool HasMore => Offset + Count < TotalCount;

    /// <summary>Whether there are items before this page.</summary>
    public bool HasPrevious => Offset > 0;
}
