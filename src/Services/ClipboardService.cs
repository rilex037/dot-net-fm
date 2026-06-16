using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace DotNetFM;

/// <summary>
/// Abstraction over the OS clipboard for file drop lists.
/// Tracks whether the last clipboard operation was cut (move) or copy.
/// No UI dependencies beyond the standard WPF System.Windows.Clipboard.
/// </summary>
public sealed class ClipboardService
{
    /// <summary>Whether the last clipboard push was a cut operation.</summary>
    public bool IsCut { get; private set; }

    /// <summary>
    /// Copies the specified file/directory paths to the clipboard as a file drop list,
    /// marking the operation as a copy.
    /// </summary>
    public void Copy(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        IsCut = false;
        Clipboard.SetFileDropList(CreateStringCollection(paths));
    }

    /// <summary>
    /// Copies the specified file/directory paths to the clipboard as a file drop list,
    /// marking the operation as a cut.
    /// </summary>
    public void Cut(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        IsCut = true;
        Clipboard.SetFileDropList(CreateStringCollection(paths));
    }

    /// <summary>
    /// Retrieves the file drop list from the clipboard, or null if none is present.
    /// </summary>
    public IReadOnlyList<string>? TryGetFileDropList()
    {
        if (!Clipboard.ContainsFileDropList()) return null;

        var raw = Clipboard.GetFileDropList();
        if (raw.Count == 0) return null;

        var result = new string[raw.Count];
        raw.CopyTo(result, 0);
        return result;
    }

    /// <summary>Resets the cut flag after a paste has been performed.</summary>
    public void ResetCutFlag() => IsCut = false;

    private static StringCollection CreateStringCollection(IReadOnlyList<string> paths)
    {
        var collection = new StringCollection();
        foreach (var p in paths)
            collection.Add(p);
        return collection;
    }
}
