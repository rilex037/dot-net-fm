using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace DotNetFM;

public static class AppContextMenuService
{
    private const uint SHOP_FILEPATH = 0x02;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(IntPtr hwnd, uint dwFlags, string pszObject, string? pszPage);

    public static void ShowSelection(Window owner, Point screenPoint, IReadOnlyList<string> paths, string? currentDirectory, IAppContextMenuHost host)
    {
        if (paths.Count == 0) return;

        var menu = CreateMenu();

        if (RecycleBinService.IsRecycleBin(currentDirectory))
        {
            if (Visible("restore"))
                menu.Items.Add(Item("_Restore", null, () => host.RestoreFromRecycleBin(paths), isDefault: true));

            if (Visible("delete"))
                menu.Items.Add(Item("_Delete Permanently", "Del", () => host.DeleteFromRecycleBin(paths)));

            OpenMenu(owner, menu);
            return;
        }

        bool single = paths.Count == 1;
        string first = paths[0];
        bool firstIsFolder = Directory.Exists(first) && !File.Exists(first);
        bool canPaste = host.CanPaste();

        if (Visible("open"))
            menu.Items.Add(Item("_Open", "Enter", () => host.OpenPaths(paths)));

        if (single && firstIsFolder && Visible("opentab"))
            menu.Items.Add(Item("Open in New _Tab", null, () => host.OpenPathInNewTab(first)));

        AddSeparator(menu);

        if (Visible("cut"))
            menu.Items.Add(Item("Cu_t", "Ctrl+X", () => host.CutPaths(paths)));

        if (Visible("copy"))
            menu.Items.Add(Item("_Copy", "Ctrl+C", () => host.CopyPaths(paths)));

        if (single && firstIsFolder && Visible("paste"))
            menu.Items.Add(Item("_Paste", "Ctrl+V", () => host.PasteInto(first), canPaste));

        bool addedArchiveGroup = false;
        string? selectionDir = Path.GetDirectoryName(first);
        bool selectionInRealFolder = selectionDir != null && Directory.Exists(selectionDir);

        if (single && selectionInRealFolder && ArchiveFormats.IsArchive(first) && Visible("extract"))
        {
            AddSeparator(menu);
            addedArchiveGroup = true;
            AddExtractItems(menu, first, host);
        }

        if (selectionInRealFolder && Visible("compress"))
        {
            if (!addedArchiveGroup)
                AddSeparator(menu);
            AddCompressItems(menu, paths, host);
        }

        AddSeparator(menu);

        if (single && Visible("rename"))
            menu.Items.Add(Item("Re_name", "F2", host.RenameSelection));

        if (Visible("delete"))
            menu.Items.Add(Item("_Delete", "Del", () => host.DeletePaths(paths)));

        if (Visible("copypath"))
            menu.Items.Add(Item("Copy _as Path", null, () => host.CopyPathsAsText(paths)));

        AddSeparator(menu);

        if (single && firstIsFolder && Visible("terminal"))
            menu.Items.Add(Item("Open in _Terminal", null, () => host.OpenTerminal(first)));

        if (Visible("properties"))
            menu.Items.Add(Item("P_roperties", null, () => host.ShowProperties(first)));

        if (Visible("shell.selection"))
            menu.Items.Add(Item("Windows _Shell Menu", null, () => host.ShowShellMenuForPaths(screenPoint, paths)));

        OpenMenu(owner, menu);
    }

    public static void ShowBackground(Window owner, Point screenPoint, string directory, bool isVirtualRoot, IAppContextMenuHost host)
    {
        var menu = CreateMenu();

        if (isVirtualRoot)
        {
            menu.Items.Add(Item("_Refresh", "F5", host.RefreshView));
            menu.Items.Add(Item("Select _All", "Ctrl+A", host.SelectAllItems));
            menu.Items.Add(Item("Show _Hidden Files", null, host.ToggleHiddenFiles, true, host.HiddenFilesShown));
            OpenMenu(owner, menu);
            return;
        }

        bool canPaste = host.CanPaste();

        if (Visible("new"))
        {
            var newMenu = Submenu("_New");
            newMenu.Items.Add(Item("_Folder", null, () => host.CreateNewFolder(directory)));
            newMenu.Items.Add(Item("_Text Document", null, () => host.CreateNewTextDocument(directory)));
            menu.Items.Add(newMenu);
        }

        if (Visible("paste"))
            menu.Items.Add(Item("_Paste", "Ctrl+V", () => host.PasteInto(directory), canPaste));

        AddSeparator(menu);

        if (Visible("sort"))
        {
            string currentKey = host.CurrentSortKey;
            bool ascending = host.CurrentSortAscending;

            var sortMenu = Submenu("Sort _By");
            sortMenu.Items.Add(Item("_Name", null, () => host.ApplySort("name", ascending), true, currentKey == "name"));
            sortMenu.Items.Add(Item("_Size", null, () => host.ApplySort("size", ascending), true, currentKey == "size"));
            sortMenu.Items.Add(Item("_Type", null, () => host.ApplySort("type", ascending), true, currentKey == "type"));
            sortMenu.Items.Add(Item("Date _Modified", null, () => host.ApplySort("date", ascending), true, currentKey == "date"));
            sortMenu.Items.Add(new Separator { Style = SeparatorStyle() });
            sortMenu.Items.Add(Item("_Ascending", null, () => host.ApplySort(currentKey, true), true, ascending));
            sortMenu.Items.Add(Item("_Descending", null, () => host.ApplySort(currentKey, false), true, !ascending));
            menu.Items.Add(sortMenu);
        }

        AddSeparator(menu);

        if (Visible("refresh"))
            menu.Items.Add(Item("_Refresh", "F5", host.RefreshView));

        if (Visible("selectall"))
            menu.Items.Add(Item("Select _All", "Ctrl+A", host.SelectAllItems));

        if (Visible("hidden"))
            menu.Items.Add(Item("Show _Hidden Files", "Ctrl+H", host.ToggleHiddenFiles, true, host.HiddenFilesShown));

        AddSeparator(menu);

        if (Visible("terminal"))
            menu.Items.Add(Item("Open in _Terminal", null, () => host.OpenTerminal(directory)));

        if (Visible("properties"))
            menu.Items.Add(Item("P_roperties", null, () => host.ShowProperties(directory)));

        if (Visible("shell.background", fallback: false))
            menu.Items.Add(Item("Windows _Shell Menu", null, () => host.ShowShellMenuForDirectory(screenPoint, directory)));

        OpenMenu(owner, menu);
    }

    public static void ShowForBookmark(Window owner, BookmarkEntry bookmark, Action open, Action openInNewTab, Action rename, Action remove)
    {
        var menu = CreateMenu();
        menu.Items.Add(Item("_Open", null, open));
        menu.Items.Add(Item("Open in New _Tab", null, openInNewTab));
        AddSeparator(menu);
        menu.Items.Add(Item("Re_name…", null, rename));
        menu.Items.Add(Item("Re_move", null, remove));
        OpenMenu(owner, menu);
    }

    public static void ShowForBookmarksSection(Window owner, Action addCurrent, Action manage)
    {
        var menu = CreateMenu();
        menu.Items.Add(Item("_Bookmark This Location…", "Ctrl+D", addCurrent));
        menu.Items.Add(Item("_Manage Bookmarks…", null, manage));
        OpenMenu(owner, menu);
    }

    public static void LaunchTerminal(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;

        string command = AppStore.Read("terminal.command");
        if (string.IsNullOrWhiteSpace(command))
            command = "powershell.exe -NoExit -Command Set-Location -LiteralPath '%V'";

        try
        {
            (string fileName, string arguments) = SplitCommand(command);

            if (arguments.Contains("%V") || arguments.Contains("%v"))
                arguments = arguments.Replace("%V", directory).Replace("%v", directory);
            else
                arguments = string.IsNullOrWhiteSpace(arguments) ? $"\"{directory}\"" : $"{arguments} \"{directory}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                WorkingDirectory = directory
            });
        }
        catch (Exception ex)
        {
            CustomDialog.Show(
                "Open in Terminal",
                $"Could not launch the terminal:\n{ex.Message}\n\nCheck the terminal command in Settings.",
                DialogIcon.Error);
        }
    }

    public static void ShowShellProperties(Window owner, string path)
    {
        try
        {
            var hwnd = new WindowInteropHelper(owner).Handle;
            if (!SHObjectProperties(hwnd, SHOP_FILEPATH, path, null))
                CustomDialog.Show("Properties", "Could not open the properties dialog.", DialogIcon.Error);
        }
        catch (Exception ex)
        {
            CustomDialog.Show("Properties", ex.Message, DialogIcon.Error);
        }
    }

    private static void AddExtractItems(ContextMenu menu, string archive, IAppContextMenuHost host)
    {
        string baseName = ArchiveFormats.GetBaseName(archive);
        string defaultAction = AppStore.Read("archives.extract.default");
        if (defaultAction.Length == 0) defaultAction = "here";

        var entries = new (string Id, string Header, ArchiveExtractMode Mode)[]
        {
            ("here", "Extract _Here", ArchiveExtractMode.Here),
            ("subfolder", $"Extract to \u201c{baseName}\\\u201d", ArchiveExtractMode.Subfolder),
            ("browse", "Extract to\u2026", ArchiveExtractMode.PickFolder)
        };

        foreach (var entry in entries.OrderByDescending(e => e.Id.Equals(defaultAction, StringComparison.OrdinalIgnoreCase)))
            menu.Items.Add(Item(entry.Header, null, () => host.ExtractArchive(archive, entry.Mode), isDefault: entry.Id.Equals(defaultAction, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AddCompressItems(ContextMenu menu, IReadOnlyList<string> paths, IAppContextMenuHost host)
    {
        string baseName = ArchiveUi.SuggestArchiveBaseName(paths);
        string defaultFormat = ArchiveUi.ResolveDefaultFormatId();

        var formats = ArchiveUi.AvailableCompressFormats()
            .OrderByDescending(f => f.Id.Equals(defaultFormat, StringComparison.OrdinalIgnoreCase));

        foreach (var (id, label) in formats)
        {
            string formatId = id;
            menu.Items.Add(Item(
                $"Compress to \u201c{baseName}{ArchiveService.GetArchiveExtension(ArchiveUi.ParseFormatId(formatId))}\u201d",
                null,
                () => host.CompressItems(paths, formatId)));
        }

        menu.Items.Add(Item("Compress to _Archive\u2026", null, () => host.CompressItems(paths, null)));
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        command = command.Trim();

        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            if (end > 0)
                return (command[1..end], command[(end + 1)..].Trim());
        }

        int space = command.IndexOf(' ');
        if (space < 0)
            return (command, "");

        return (command[..space], command[(space + 1)..].Trim());
    }

    private static bool Visible(string key, bool fallback = true) =>
        AppStore.ReadBool("contextmenu." + key, fallback);

    private static ContextMenu CreateMenu() => new()
    {
        Style = (Style)Application.Current.FindResource("ThemedContextMenu")
    };

    private static MenuItem Item(string header, string? gesture, Action action, bool isEnabled = true, bool isChecked = false, bool isDefault = false)
    {
        var item = new MenuItem
        {
            Header = header,
            Style = (Style)Application.Current.FindResource("ThemedMenuItem"),
            IsEnabled = isEnabled,
            IsChecked = isChecked
        };

        if (isDefault)
            item.FontWeight = FontWeights.Bold;

        if (gesture != null)
            item.InputGestureText = gesture;

        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem Submenu(string header)
    {
        return new MenuItem
        {
            Header = header,
            Style = (Style)Application.Current.FindResource("ThemedMenuItem")
        };
    }

    private static void AddSeparator(ContextMenu menu)
    {
        if (menu.Items.Count == 0) return;
        if (menu.Items[^1] is Separator) return;
        menu.Items.Add(new Separator { Style = SeparatorStyle() });
    }

    private static Style SeparatorStyle() => (Style)Application.Current.FindResource("ThemedSeparator");

    private static void OpenMenu(Window owner, ContextMenu menu)
    {
        if (menu.Items.Count == 0) return;

        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
}
