namespace dot_net_fm;

/// <summary>
/// Represents the loading state of a tab's directory content.
/// </summary>
public enum TabLoadStatus
{
    /// <summary>No active loading operation.</summary>
    Idle,

    /// <summary>Actively loading directory contents for a new navigation.</summary>
    Loading,

    /// <summary>Refreshing the current directory (e.g. F5 or file-system change).</summary>
    Refreshing,

    /// <summary>An error occurred while loading the directory.</summary>
    Error
}