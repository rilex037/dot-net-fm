using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>Defines the set of buttons shown on the dialog.</summary>
public enum DialogButtons
{
    /// <summary>Only an OK button.</summary>
    OK,
    /// <summary>OK and Cancel buttons.</summary>
    OKCancel,
    /// <summary>Yes and No buttons.</summary>
    YesNo,
    /// <summary>Yes, No, and Cancel buttons.</summary>
    YesNoCancel,
}

/// <summary>Result returned from a dialog with multiple buttons.</summary>
public enum CustomDialogResult
{
    /// <summary>Dialog was closed without a selection (e.g. Escape pressed).</summary>
    None,
    OK,
    Cancel,
    Yes,
    No,
}

/// <summary>Defines the type of icon shown on the dialog.</summary>
public enum DialogIcon
{
    /// <summary>No icon displayed.</summary>
    None,
    /// <summary>Red circle with exclamation mark.</summary>
    Error,
    /// <summary>Yellow circle with question mark.</summary>
    Confirmation,
    /// <summary>Light gray circle with "i".</summary>
    Info,
    /// <summary>Green circle with checkmark.</summary>
    Success,
}

/// <summary>
/// A custom-themed modal dialog that replaces <c>MessageBox</c> and the old <c>AlertBar</c> overlay.
/// Displays an SVG icon (error, confirmation, info, or success), a title, a message,
/// and dynamically-created buttons. Blocks synchronously via <see cref="ShowDialog"/>.
/// <para>
/// Usage:
/// <code>CustomDialog.Show("Error", "File not found.");</code>
/// <code>var result = CustomDialog.Show("Confirm", "Delete?", DialogIcon.Confirmation, DialogButtons.YesNo);</code>
/// </para>
/// </summary>
public partial class CustomDialog : Window
{
    private readonly DialogButtons _buttons;
    private CustomDialogResult _result = CustomDialogResult.None;

    // ── Private constructor — use static Show methods ───────────────

    private CustomDialog(string title, string message, DialogIcon icon, DialogButtons buttons)
    {
        InitializeComponent();

        _buttons = buttons;

        TitleText.Text = title;
        MessageText.Text = message;

        // Set icon
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

        // Create buttons
        CreateButtons();

        // Allow Escape to close (returns None)
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                _result = CustomDialogResult.None;
                Close();
            }
        };
    }

    // ── Static Show methods ─────────────────────────────────────────

    /// <summary>
    /// Shows a dialog with an OK button (informational/error).
    /// </summary>
    public static CustomDialogResult Show(string title, string message, DialogIcon icon = DialogIcon.None)
    {
        return Show(title, message, icon, DialogButtons.OK);
    }

    /// <summary>
    /// Shows a dialog with configurable buttons (e.g. YesNoCancel).
    /// Blocks until the user dismisses it. Returns the chosen <see cref="CustomDialogResult"/>.
    /// </summary>
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

    // ── Button creation ─────────────────────────────────────────────

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
            MinWidth = 80,
            Height = 30,
            Margin = new Thickness(6, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel = result == CustomDialogResult.Cancel || result == CustomDialogResult.None,
        };

        button.Click += (_, _) =>
        {
            _result = result;
            Close();
        };

        ButtonPanel.Children.Add(button);
    }

    // ── Icon mapping ────────────────────────────────────────────────

    private static string? GetIconPath(DialogIcon icon) => icon switch
    {
        DialogIcon.Error => IconProvider.GetFullPath("dialogue-error.svg"),
        DialogIcon.Confirmation => IconProvider.GetFullPath("dialogue-confirmation.svg"),
        DialogIcon.Info => IconProvider.GetFullPath("dialogue-info.svg"),
        DialogIcon.Success => IconProvider.GetFullPath("dialogue-success.svg"),
        _ => null,
    };
}