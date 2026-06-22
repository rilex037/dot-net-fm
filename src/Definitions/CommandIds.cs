using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Central definition of all application-level keyboard shortcuts.
/// Menu items bind to these commands so gesture text is never duplicated.
/// </summary>
public static class CommandIds
{
    public static readonly RoutedUICommand Exit       = new("Exit",       "Exit",       typeof(CommandIds), [ new KeyGesture(Key.F4, ModifierKeys.Alt) ]);
    public static readonly RoutedUICommand Rename     = new("Rename",     "Rename",     typeof(CommandIds), [ new KeyGesture(Key.F2) ]);
    public static readonly RoutedUICommand Delete     = new("Delete",     "Delete",     typeof(CommandIds), [ new KeyGesture(Key.Delete) ]);
    public static readonly RoutedUICommand Copy       = new("Copy",       "Copy",       typeof(CommandIds), [ new KeyGesture(Key.C, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand Cut        = new("Cut",        "Cut",        typeof(CommandIds), [ new KeyGesture(Key.X, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand Paste      = new("Paste",      "Paste",      typeof(CommandIds), [ new KeyGesture(Key.V, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand Refresh    = new("Refresh",    "Refresh",    typeof(CommandIds), [ new KeyGesture(Key.F5) ]);
    public static readonly RoutedUICommand ZoomIn     = new("ZoomIn",     "ZoomIn",     typeof(CommandIds), [ new KeyGesture(Key.OemPlus, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand ZoomOut    = new("ZoomOut",    "ZoomOut",    typeof(CommandIds), [ new KeyGesture(Key.OemMinus, ModifierKeys.Control) ]);
    public static readonly RoutedUICommand SelectAll  = new("SelectAll",  "SelectAll",  typeof(CommandIds), [ new KeyGesture(Key.A, ModifierKeys.Control) ]);
}
