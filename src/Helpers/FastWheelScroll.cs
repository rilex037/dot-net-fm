using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Chrome-style wheel scrolling: a fixed distance per notch that ramps up while the wheel keeps
/// spinning. Offsets are applied immediately, with no animation or inertia.
/// </summary>
public sealed class FastWheelScroll
{
    private const double PixelsPerNotch = 120;
    private const double BoostStep = 0.4;
    private const double MaxBoost = 3.5;
    private const long BoostWindowMs = 100;

    private long _lastTick;
    private int _lastDirection;
    private double _boost = 1;

    public void Apply(ScrollViewer viewer, MouseWheelEventArgs e)
    {
        if (e.Delta == 0)
            return;

        int direction = Math.Sign(e.Delta);
        long now = Environment.TickCount64;

        // Only discrete wheel notches accelerate. Precision touchpads send many small deltas
        // and are scrolled 1:1.
        bool isNotch = Math.Abs(e.Delta) >= 120;

        if (isNotch && direction == _lastDirection && now - _lastTick <= BoostWindowMs)
            _boost = Math.Min(_boost + BoostStep, MaxBoost);
        else
            _boost = 1;

        _lastDirection = direction;
        _lastTick = now;

        double pixels = e.Delta / 120.0 * PixelsPerNotch * (isNotch ? _boost : 1);
        viewer.ScrollToVerticalOffset(Math.Round(viewer.VerticalOffset - pixels));
    }
}
