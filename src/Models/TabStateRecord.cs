using System;

namespace dot_net_fm;

/// <summary>
/// Immutable snapshot of a tab's UI-facing state.
/// Changes only flow through the reducer — never set directly.
/// </summary>
public sealed record TabStateRecord
{
    /// <summary>Unique identifier for this tab.</summary>
    public Guid TabId { get; init; } = Guid.NewGuid();

    /// <summary>Current directory path being displayed.</summary>
    public string ActivePath { get; init; } = "";

    /// <summary>Display name for the tab header (folder name).</summary>
    public string Title { get; init; } = "New Tab";

    /// <summary>Path shown in the address bar.</summary>
    public string DisplayPath { get; init; } = "";

    /// <summary>Current loading status of the tab.</summary>
    public TabLoadStatus LoadStatus { get; init; } = TabLoadStatus.Idle;

    /// <summary>Current icon size (zoom level) in pixels.</summary>
    public int IconSize { get; init; } = 64;

    /// <summary>Whether there is history to go back to.</summary>
    public bool CanGoBack { get; init; }

    /// <summary>Whether there is history to go forward to.</summary>
    public bool CanGoForward { get; init; }

    /// <summary>Whether there is a parent directory to navigate up to.</summary>
    public bool CanGoUp { get; init; }

    /// <summary>Number of items currently displayed in the grid.</summary>
    public int ItemCount { get; init; }

    /// <summary>Status text shown in the status bar (e.g. "42 items, Free space: 128.3 GB").</summary>
    public string StatusText { get; init; } = "";
}
