using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

public partial class CompressDialog : Window
{
    private readonly List<(string Id, string Label)> _formats;
    private readonly Dictionary<string, RadioButton> _formatButtons = new(StringComparer.OrdinalIgnoreCase);

    private CompressDialog(string title, string defaultName, IReadOnlyList<(string Id, string Label)> formats, string defaultFormatId)
    {
        InitializeComponent();
        TitleText.Text = title;
        _formats = [.. formats];

        foreach (var (id, label) in _formats)
        {
            var radio = new RadioButton
            {
                Content = label,
                GroupName = "ArchiveFormat",
                IsChecked = id.Equals(defaultFormatId, StringComparison.OrdinalIgnoreCase),
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                FontSize = (double)FindResource("FontSidebarItemSize"),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };

            _formatButtons[id] = radio;
            FormatPanel.Children.Add(radio);
        }

        NameBox.Text = defaultName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public static (string Name, string FormatId)? Show(Window? owner, string defaultName,
        IReadOnlyList<(string Id, string Label)> formats, string defaultFormatId)
    {
        var dialog = new CompressDialog("Compress to Archive", defaultName, formats, defaultFormatId)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() != true) return null;

        string name = dialog.NameBox.Text.Trim();
        if (name.Length == 0) return null;

        string formatId = defaultFormatId;
        foreach (var (id, _) in dialog._formats)
        {
            if (dialog._formatButtons[id].IsChecked == true)
            {
                formatId = id;
                break;
            }
        }

        return (name, formatId);
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
