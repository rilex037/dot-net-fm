using System.Windows.Input;

namespace DotNetFM;

public static class CommandIds
{
    public static readonly RoutedUICommand Exit       = new("Exit",       "Exit",       typeof(CommandIds), [ new KeyGesture(Key.F4, ModifierKeys.Alt) ]);
    public static readonly RoutedUICommand Rename     = new("Rename",     "Rename",     typeof(CommandIds), [ new KeyGesture(Key.F2) ]);
    public static readonly RoutedUICommand Delete     = new("Delete",     "Delete",     typeof(CommandIds), [ new KeyGesture(Key.Delete) ]);
    public static readonly RoutedUICommand Copy       = new("Copy",       "Copy",       typeof(CommandIds), [ new KeyGesture(Key.C, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand Cut        = new("Cut",        "Cut",        typeof(CommandIds), [ new KeyGesture(Key.X, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand Paste      = new("Paste",      "Paste",      typeof(CommandIds), [ new KeyGesture(Key.V, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand SelectAll  = new("SelectAll",  "SelectAll",  typeof(CommandIds), [ new KeyGesture(Key.A, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand Refresh    = new("Refresh",    "Refresh",    typeof(CommandIds), [ new KeyGesture(Key.F5) ]);
    public static readonly RoutedUICommand ZoomIn     = new("ZoomIn",     "ZoomIn",     typeof(CommandIds), [ new KeyGesture(Key.OemPlus, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand ZoomOut    = new("ZoomOut",    "ZoomOut",    typeof(CommandIds), [ new KeyGesture(Key.OemMinus, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand NewTab     = new("New Tab",    "NewTab",     typeof(CommandIds), [ new KeyGesture(Key.T, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand CloseTab   = new("Close Tab",  "CloseTab",   typeof(CommandIds), [ new KeyGesture(Key.W, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand NextTab    = new("Next Tab",   "NextTab",    typeof(CommandIds), [ new KeyGesture(Key.Tab, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand PrevTab    = new("Prev Tab",   "PrevTab",    typeof(CommandIds), [ new KeyGesture(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift) ]);
    public static readonly RoutedUICommand GoBack     = new("Back",       "GoBack",     typeof(CommandIds), [ new KeyGesture(Key.Left, ModifierKeys.Alt) ]);
    public static readonly RoutedUICommand GoForward  = new("Forward",    "GoForward",  typeof(CommandIds), [ new KeyGesture(Key.Right, ModifierKeys.Alt) ]);
    public static readonly RoutedUICommand GoUp       = new("Up",         "GoUp",       typeof(CommandIds), [ new KeyGesture(Key.Back) ]);
    public static readonly RoutedUICommand Open       = new("Open",       "Open",       typeof(CommandIds), [ new KeyGesture(Key.Enter) ]);
    public static readonly RoutedUICommand ClearSelection = new("Clear Selection", "ClearSelection", typeof(CommandIds), [ new KeyGesture(Key.Escape) ]);
    public static readonly RoutedUICommand FocusAddress   = new("Focus Address Bar", "FocusAddress", typeof(CommandIds), [ new KeyGesture(Key.L, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand AddBookmark    = new("Bookmark This Location", "AddBookmark", typeof(CommandIds), [ new KeyGesture(Key.D, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand ToggleBookmarksSection = new("Bookmarks Section", "ToggleBookmarksSection", typeof(CommandIds), [ new KeyGesture(Key.B, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand ToggleHiddenFiles  = new("Show Hidden Files", "ToggleHiddenFiles", typeof(CommandIds), [ new KeyGesture(Key.H, ModifierKeys.Control) ]);
}
