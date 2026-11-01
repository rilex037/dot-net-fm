using System.Windows;
using System.Windows.Input;

namespace DotNetFM;

public partial class BookmarkManagerDialog : Window
{
    private readonly List<BookmarkEntry> _entries;

    private BookmarkManagerDialog(IReadOnlyList<BookmarkEntry> entries)
    {
        InitializeComponent();
        _entries = entries.Select(e => new BookmarkEntry(e.Name, e.Path)).ToList();
        BookmarksList.ItemsSource = _entries;
    }

    public static List<BookmarkEntry>? Show(IReadOnlyList<BookmarkEntry> entries)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dialog = new BookmarkManagerDialog(entries) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog._entries : null;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        int index = BookmarksList.SelectedIndex;
        if (index <= 0) return;

        (_entries[index - 1], _entries[index]) = (_entries[index], _entries[index - 1]);
        RefreshList(index - 1);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        int index = BookmarksList.SelectedIndex;
        if (index < 0 || index >= _entries.Count - 1) return;

        (_entries[index], _entries[index + 1]) = (_entries[index + 1], _entries[index]);
        RefreshList(index + 1);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        int index = BookmarksList.SelectedIndex;
        if (index < 0) return;

        string? name = InputDialog.Show("Rename Bookmark", "Bookmark name:", _entries[index].Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        _entries[index] = new BookmarkEntry(name.Trim(), _entries[index].Path);
        RefreshList(index);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        int index = BookmarksList.SelectedIndex;
        if (index < 0) return;

        _entries.RemoveAt(index);
        RefreshList(Math.Min(index, _entries.Count - 1));
    }

    private void RefreshList(int selectIndex)
    {
        BookmarksList.ItemsSource = null;
        BookmarksList.ItemsSource = _entries;
        if (selectIndex >= 0 && selectIndex < _entries.Count)
            BookmarksList.SelectedIndex = selectIndex;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
