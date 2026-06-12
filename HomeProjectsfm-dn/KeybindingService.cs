using System;
using System.Windows.Input;

namespace FmDn;

/// <summary>
/// Handles keyboard shortcut dispatch. No UI dependencies — only key identification and events.
/// </summary>
public class KeybindingService
{
    public event Action? F2Pressed;
    public event Action? DeletePressed;
    public event Action? F5Pressed;
    public event Action<bool>? ZoomRequested; // true = zoom in, false = zoom out

    /// <summary>
    /// Processes a key event and raises the appropriate action.
    /// Returns true if the key was handled.
    /// </summary>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F2:
                F2Pressed?.Invoke();
                e.Handled = true;
                return true;
            case Key.Delete:
                DeletePressed?.Invoke();
                e.Handled = true;
                return true;
            case Key.F5:
                F5Pressed?.Invoke();
                e.Handled = true;
                return true;
            default:
                return false;
        }
    }

    public bool HandleMouseWheel(MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            ZoomRequested?.Invoke(e.Delta > 0);
            e.Handled = true;
            return true;
        }
        return false;
    }
}
