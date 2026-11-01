using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

public enum DialogButtons
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel,
}

public enum CustomDialogResult
{
    None,
    OK,
    Cancel,
    Yes,
    No,
}

public enum DialogIcon
{
    None,
    Error,
    Confirmation,
    Info,
    Success,
}

public partial class CustomDialog : Window
{
    private readonly DialogButtons _buttons;
    private CustomDialogResult _result = CustomDialogResult.None;

    private CustomDialog(string title, string message, DialogIcon icon, DialogButtons buttons)
    {
        InitializeComponent();

        _buttons = buttons;

        TitleText.Text = title;
        MessageText.Text = message;

        var iconPath = GetIconPath(icon);
        if (iconPath != null)
        {
            IconImage.UriSource = new Uri(iconPath);
            IconContainer.Visibility = Visibility.Visible;
        }
        else
        {
            IconContainer.Visibility = Visibility.Collapsed;
        }

        CreateButtons();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                _result = CustomDialogResult.None;
                Close();
            }
        };
    }

    public static CustomDialogResult Show(string title, string message, DialogIcon icon = DialogIcon.None)
    {
        return Show(title, message, icon, DialogButtons.OK);
    }

    public static CustomDialogResult Show(string title, string message, DialogIcon icon, DialogButtons buttons)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dialog = new CustomDialog(title, message, icon, buttons)
        {
            Owner = owner
        };

        dialog.ShowDialog();
        return dialog._result;
    }

    private void CreateButtons()
    {
        switch (_buttons)
        {
            case DialogButtons.OK:
                AddButton("OK", CustomDialogResult.OK, isDefault: true);
                break;

            case DialogButtons.OKCancel:
                AddButton("OK", CustomDialogResult.OK, isDefault: true);
                AddButton("Cancel", CustomDialogResult.Cancel);
                break;

            case DialogButtons.YesNo:
                AddButton("Yes", CustomDialogResult.Yes, isDefault: true);
                AddButton("No", CustomDialogResult.No);
                break;

            case DialogButtons.YesNoCancel:
                AddButton("Yes", CustomDialogResult.Yes, isDefault: true);
                AddButton("No", CustomDialogResult.No);
                AddButton("Cancel", CustomDialogResult.Cancel);
                break;
        }
    }

    private void AddButton(string text, CustomDialogResult result, bool isDefault = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 88,
            Height = 30,
            Margin = new Thickness(6, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel = result == CustomDialogResult.Cancel || result == CustomDialogResult.None,
            Style = (Style)FindResource("DialogButton"),
        };

        button.Click += (_, _) =>
        {
            _result = result;
            Close();
        };

        ButtonPanel.Children.Add(button);
    }

    private static string? GetIconPath(DialogIcon icon) => icon switch
    {
        DialogIcon.Error => IconProvider.GetFullPath("dialogue-error.svg"),
        DialogIcon.Confirmation => IconProvider.GetFullPath("dialogue-confirmation.svg"),
        DialogIcon.Info => IconProvider.GetFullPath("dialogue-info.svg"),
        DialogIcon.Success => IconProvider.GetFullPath("dialogue-success.svg"),
        _ => null,
    };
}
