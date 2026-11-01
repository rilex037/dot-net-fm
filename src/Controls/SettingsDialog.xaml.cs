using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

public partial class SettingsDialog : Window
{
    private const string DefaultTerminalCommand = "powershell.exe -NoExit -Command Set-Location -LiteralPath '%V'";

    private sealed record OptionEntry(string Key, string Label, string Description, bool Default);

    private sealed record ArchiveChoice(string Id, string Label);

    private static readonly OptionEntry[] Entries =
    [
        new("open", "Open", "Selection menu: open the selected item.", true),
        new("opentab", "Open in New Tab", "Selection menu: folders open in a new tab.", true),
        new("cut", "Cut", "Selection menu: cut (Ctrl+X).", true),
        new("copy", "Copy", "Selection menu: copy (Ctrl+C).", true),
        new("paste", "Paste", "Selection and background menu: paste (Ctrl+V).", true),
        new("extract", "Extract", "Selection menu: extract archives (here, subfolder, to…).", true),
        new("compress", "Compress", "Selection menu: compress the selection into an archive.", true),
        new("rename", "Rename", "Selection menu: rename (F2).", true),
        new("delete", "Delete", "Selection menu: delete (Del).", true),
        new("restore", "Restore", "Recycle Bin menu: restore selected items to their original location.", true),
        new("copypath", "Copy as Path", "Selection menu: copy the full path as text.", true),
        new("new", "New", "Background menu: new folder and text document.", true),
        new("sort", "Sort By", "Background menu: sort by name, size, type or date — per tab, remembered across restarts.", true),
        new("refresh", "Refresh", "Background menu: refresh (F5).", true),
        new("selectall", "Select All", "Background menu: select all (Ctrl+A).", true),
        new("hidden", "Show Hidden Files", "Background menu: toggle hidden files (Ctrl+H).", true),
        new("terminal", "Open in Terminal", "Launch your own terminal in this folder.", true),
        new("properties", "Properties", "Windows properties dialog for the item or folder.", true),
        new("shell.selection", "Windows Shell Menu (selection)", "Explorer menu for selected items (7-Zip, Open with, ...).", true),
        new("shell.background", "Windows Shell Menu (background)", "Explorer menu for the folder background.", false)
    ];

    private readonly Dictionary<string, CheckBox> _checkboxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RadioButton>> _archiveRadios = new(StringComparer.OrdinalIgnoreCase);

    private SettingsDialog()
    {
        InitializeComponent();
        BuildOptions();
        BuildArchiveOptions();
        TerminalCommandBox.Text = AppStore.Read("terminal.command");
        if (string.IsNullOrWhiteSpace(TerminalCommandBox.Text))
            TerminalCommandBox.Text = DefaultTerminalCommand;
    }

    public static void Present()
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dialog = new SettingsDialog { Owner = owner };
        dialog.ShowDialog();
    }

    private void BuildOptions()
    {
        ContextMenuOptions.Children.Clear();
        _checkboxes.Clear();

        foreach (var entry in Entries)
        {
            var box = new CheckBox
            {
                Content = entry.Label,
                ToolTip = entry.Description,
                IsChecked = AppStore.ReadBool("contextmenu." + entry.Key, entry.Default),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                FontSize = (double)FindResource("FontSidebarItemSize"),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            _checkboxes[entry.Key] = box;
            ContextMenuOptions.Children.Add(box);
        }
    }

    private void BuildArchiveOptions()
    {
        ArchiveOptions.Children.Clear();
        _archiveRadios.Clear();

        var engines = ArchiveEngineFinder.Detect();

        string autoLabel = engines.Count > 0
            ? $"Automatic — use {engines[0].DisplayName} when available"
            : "Automatic — no external archiver found";

        var engineChoices = new List<ArchiveChoice> { new("auto", autoLabel), new("builtin", "Built-in only (ZIP, TAR, GZIP)") };
        engineChoices.AddRange(engines.Select(e => new ArchiveChoice(e.Id, e.DisplayName)));

        AddArchiveGroup("archive.engine", "Archive engine", engineChoices, "auto");
        AddArchiveGroup("archive.extract.default", "Default extract action",
        [
            new ArchiveChoice("here", "Extract Here"),
            new ArchiveChoice("subfolder", "Extract to a subfolder"),
            new ArchiveChoice("browse", "Extract to…")
        ], "here");

        var formatChoices = new List<ArchiveChoice> { new("zip", "ZIP"), new("targz", "TAR.GZ") };
        if (engines.Any(e => e.Kind == ArchiveEngineKind.SevenZipFamily))
            formatChoices.Insert(1, new ArchiveChoice("7z", "7Z (via 7-Zip or NanaZip)"));
        if (engines.Any(e => e.Kind == ArchiveEngineKind.WinRar))
            formatChoices.Add(new ArchiveChoice("rar", "RAR (via WinRAR)"));

        AddArchiveGroup("archive.format", "Default archive format", formatChoices, "zip");

        ArchiveDetectionText.Text = engines.Count > 0
            ? "Detected: " + string.Join("  |  ", engines.Select(e => $"{e.DisplayName} ({e.ExecutablePath})"))
            : "No external archiver found. The built-in engine handles ZIP, TAR and GZIP; install 7-Zip, NanaZip or WinRAR for 7z/RAR and everything else (detected automatically, no registry involved).";
    }

    private void AddArchiveGroup(string settingsKey, string title, List<ArchiveChoice> choices, string fallback)
    {
        ArchiveOptions.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = (double)FindResource("FontStatusSize"),
            Foreground = (System.Windows.Media.Brush)FindResource("SectionHeaderBrush"),
            Margin = new Thickness(0, 6, 0, 6)
        });

        string current = AppStore.Read(settingsKey);
        if (current.Length == 0 || choices.All(c => !c.Id.Equals(current, StringComparison.OrdinalIgnoreCase)))
            current = fallback;

        var radios = new List<RadioButton>();

        foreach (var choice in choices)
        {
            var radio = new RadioButton
            {
                Content = choice.Label,
                GroupName = settingsKey,
                IsChecked = choice.Id.Equals(current, StringComparison.OrdinalIgnoreCase),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                FontSize = (double)FindResource("FontSidebarItemSize"),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Tag = choice.Id
            };

            radios.Add(radio);
            ArchiveOptions.Children.Add(radio);
        }

        _archiveRadios[settingsKey] = radios;
    }

    private string GetArchiveChoice(string settingsKey, string fallback)
    {
        if (_archiveRadios.TryGetValue(settingsKey, out var radios))
        {
            foreach (var radio in radios)
            {
                if (radio.IsChecked == true && radio.Tag is string id)
                    return id;
            }
        }

        return fallback;
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in Entries)
            _checkboxes[entry.Key].IsChecked = entry.Default;

        TerminalCommandBox.Text = DefaultTerminalCommand;
        BuildArchiveOptions();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["terminal.command"] = TerminalCommandBox.Text.Trim(),
            ["archives.engine"] = GetArchiveChoice("archive.engine", "auto"),
            ["archives.extract.default"] = GetArchiveChoice("archive.extract.default", "here"),
            ["archives.format"] = GetArchiveChoice("archive.format", "zip")
        };

        foreach (var entry in Entries)
            changes["contextmenu." + entry.Key] = _checkboxes[entry.Key].IsChecked == true ? "1" : "0";

        AppStore.WriteMany(changes);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
