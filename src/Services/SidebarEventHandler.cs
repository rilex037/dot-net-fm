using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Routes mouse events on sidebar items to semantic actions (navigate, open-in-new-tab, etc.).
/// Encapsulates hit-testing and path validation so the UI layer stays clean.
/// </summary>
public sealed class SidebarEventHandler
{
    private readonly ItemsControl _itemsControl;
    private readonly IFileProvider? _fileProvider;

    /// <summary>Raised when the user left-clicks a sidebar item to navigate in the current tab.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>Raised when the user middle-clicks a sidebar item to open it in a new tab.</summary>
    public event Action<string>? OpenInNewTabRequested;

    public SidebarEventHandler(ItemsControl itemsControl, IFileProvider? fileProvider)
    {
        _itemsControl = itemsControl;
        _fileProvider = fileProvider;
        _itemsControl.PreviewMouseDown += OnPreviewMouseDown;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var item = VisualTreeUtility.GetSidebarItemAtPoint(_itemsControl, e.GetPosition(_itemsControl));
        if (item == null) return;

        string targetPath = item.Path;
        if (string.IsNullOrEmpty(targetPath)) return;

        if (_fileProvider?.PathExists(targetPath) != true) return;

        switch (e.ChangedButton)
        {
            case MouseButton.Left when e.ButtonState == MouseButtonState.Pressed:
                NavigateRequested?.Invoke(targetPath);
                e.Handled = true;
                break;

            case MouseButton.Middle when e.ButtonState == MouseButtonState.Pressed:
                OpenInNewTabRequested?.Invoke(targetPath);
                e.Handled = true;
                break;
        }
    }
}
