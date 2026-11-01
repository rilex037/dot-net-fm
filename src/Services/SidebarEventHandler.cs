using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

public sealed class SidebarEventHandler
{
    private readonly ItemsControl _itemsControl;
    private readonly IFileProvider? _fileProvider;

    public event Action<string>? NavigateRequested;

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
