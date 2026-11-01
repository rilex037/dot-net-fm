using System.Windows;
using System.Windows.Input;

namespace DotNetFM;

public partial class InputDialog : Window
{
    private InputDialog(string title, string label, string initial)
    {
        InitializeComponent();
        TitleText.Text = title;
        LabelText.Text = label;
        ValueBox.Text = initial;

        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public static string? Show(string title, string label, string initial = "")
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dialog = new InputDialog(title, label, initial) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.ValueBox.Text : null;
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void OK_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
