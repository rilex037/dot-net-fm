using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DotNetFM;

/// <summary>
/// Horizontal tab host. Tabs shrink evenly to fit the available width (never above their natural
/// width, never below <see cref="MinTabWidth"/>). When they can't shrink any further the strip
/// scrolls. Scroll state is exposed through read-only dependency properties so the scroll buttons
/// can bind to it; all layout is computed synchronously in Measure/Arrange.
/// </summary>
public sealed class TabOverflowPanel : Panel
{
    public static readonly DependencyProperty IsSeparatorProperty =
        DependencyProperty.RegisterAttached("IsSeparator", typeof(bool), typeof(TabOverflowPanel),
            new PropertyMetadata(false));

    public static bool GetIsSeparator(DependencyObject element) => (bool)element.GetValue(IsSeparatorProperty);
    public static void SetIsSeparator(DependencyObject element, bool value) => element.SetValue(IsSeparatorProperty, value);

    public static readonly DependencyProperty MinTabWidthProperty =
        DependencyProperty.Register(nameof(MinTabWidth), typeof(double), typeof(TabOverflowPanel),
            new FrameworkPropertyMetadata(90.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private static readonly DependencyPropertyKey IsOverflowingKey =
        DependencyProperty.RegisterReadOnly(nameof(IsOverflowing), typeof(bool), typeof(TabOverflowPanel),
            new PropertyMetadata(false));

    private static readonly DependencyPropertyKey CanScrollLeftKey =
        DependencyProperty.RegisterReadOnly(nameof(CanScrollLeft), typeof(bool), typeof(TabOverflowPanel),
            new PropertyMetadata(false));

    private static readonly DependencyPropertyKey CanScrollRightKey =
        DependencyProperty.RegisterReadOnly(nameof(CanScrollRight), typeof(bool), typeof(TabOverflowPanel),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsOverflowingProperty = IsOverflowingKey.DependencyProperty;
    public static readonly DependencyProperty CanScrollLeftProperty = CanScrollLeftKey.DependencyProperty;
    public static readonly DependencyProperty CanScrollRightProperty = CanScrollRightKey.DependencyProperty;

    public double MinTabWidth
    {
        get => (double)GetValue(MinTabWidthProperty);
        set => SetValue(MinTabWidthProperty, value);
    }

    /// <summary>True when tabs are at minimum width and still don't fit.</summary>
    public bool IsOverflowing => (bool)GetValue(IsOverflowingProperty);

    /// <summary>True when there are hidden tabs to the left of the viewport.</summary>
    public bool CanScrollLeft => (bool)GetValue(CanScrollLeftProperty);

    /// <summary>True when there are hidden tabs to the right of the viewport.</summary>
    public bool CanScrollRight => (bool)GetValue(CanScrollRightProperty);

    /// <summary>The active tab element. It is kept in view when the panel is resized.</summary>
    public UIElement? ActiveChild { get; set; }

    private double[] _slotWidths = Array.Empty<double>();
    private double _extent;
    private double _offset;
    private double _lastArrangeWidth;
    private bool _overflowing;
    private bool _revealPending;

    public TabOverflowPanel()
    {
        ClipToBounds = true;
    }

    public void ScrollBy(double delta)
    {
        if (!_overflowing) return;
        _offset += delta;
        InvalidateArrange();
    }

    public void ScrollByTabs(int direction) =>
        ScrollBy(direction * (Math.Max(MinTabWidth, 1) + 1));

    public void RevealActive()
    {
        _revealPending = true;
        InvalidateArrange();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (_overflowing)
        {
            ScrollBy(-e.Delta);
            e.Handled = true;
            return;
        }
        base.OnMouseWheel(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = InternalChildren;
        int count = children.Count;
        double height = availableSize.Height;

        var natural = new double[count];
        double sepTotal = 0, tabTotal = 0, maxHeight = 0;

        for (int i = 0; i < count; i++)
        {
            var child = children[i];
            child.Measure(new Size(double.PositiveInfinity, height));
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);

            if (GetIsSeparator(child))
            {
                natural[i] = child.DesiredSize.Width;
                sepTotal += natural[i];
            }
            else
            {
                natural[i] = Math.Max(child.DesiredSize.Width, MinTabWidth);
                tabTotal += natural[i];
            }
        }

        double avail = availableSize.Width;
        var slots = (double[])natural.Clone();
        _overflowing = false;

        if (!double.IsInfinity(avail) && tabTotal + sepTotal > avail + 0.01)
        {
            var pending = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (!GetIsSeparator(children[i]))
                    pending.Add(i);
            }

            // Water-filling: tabs shorter than the fair share keep their natural width,
            // the rest split what remains (floored at MinTabWidth).
            double remaining = avail - sepTotal;
            bool changed = true;
            while (changed && pending.Count > 0)
            {
                changed = false;
                double share = remaining / pending.Count;
                for (int p = pending.Count - 1; p >= 0; p--)
                {
                    int i = pending[p];
                    if (natural[i] <= share)
                    {
                        slots[i] = natural[i];
                        remaining -= natural[i];
                        pending.RemoveAt(p);
                        changed = true;
                    }
                }
            }

            if (pending.Count > 0)
            {
                double share = Math.Max(Math.Floor(remaining / pending.Count), MinTabWidth);
                foreach (int i in pending)
                    slots[i] = Math.Min(natural[i], share);
            }

            double used = 0;
            for (int i = 0; i < count; i++) used += slots[i];
            _overflowing = used > avail + 0.01;
        }

        _extent = 0;
        for (int i = 0; i < count; i++)
        {
            if (!GetIsSeparator(children[i]))
                children[i].Measure(new Size(slots[i], height));
            _extent += slots[i];
        }
        _slotWidths = slots;

        double width = _overflowing ? avail : double.IsInfinity(avail) ? _extent : Math.Min(_extent, avail);
        return new Size(width, double.IsInfinity(height) ? maxHeight : height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = InternalChildren;

        double viewport = finalSize.Width;
        double maxOffset = Math.Max(0, _extent - viewport);

        bool resized = Math.Abs(finalSize.Width - _lastArrangeWidth) > 0.5;
        _lastArrangeWidth = finalSize.Width;

        if ((_revealPending || resized) && ActiveChild != null)
            RevealIntoView(ActiveChild, viewport);
        _revealPending = false;

        _offset = Math.Clamp(_offset, 0, maxOffset);

        double x = -_offset;
        for (int i = 0; i < children.Count; i++)
        {
            double w = SlotWidth(i, children[i]);
            children[i].Arrange(new Rect(x, 0, w, finalSize.Height));
            x += w;
        }

        UpdateState(_overflowing, _overflowing && _offset > 0.5, _overflowing && _offset < maxOffset - 0.5);
        return finalSize;
    }

    private void RevealIntoView(UIElement target, double viewport)
    {
        var children = InternalChildren;
        double left = 0;
        for (int i = 0; i < children.Count; i++)
        {
            double w = SlotWidth(i, children[i]);
            if (ReferenceEquals(children[i], target))
            {
                if (left < _offset)
                    _offset = left;
                else if (left + w > _offset + viewport)
                    _offset = left + w - viewport;
                return;
            }
            left += w;
        }
    }

    private double SlotWidth(int index, UIElement child) =>
        index < _slotWidths.Length ? _slotWidths[index] : child.DesiredSize.Width;

    private void UpdateState(bool overflowing, bool canLeft, bool canRight)
    {
        if (IsOverflowing != overflowing) SetValue(IsOverflowingKey, overflowing);
        if (CanScrollLeft != canLeft) SetValue(CanScrollLeftKey, canLeft);
        if (CanScrollRight != canRight) SetValue(CanScrollRightKey, canRight);
    }
}
