using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DotNetFM;

/// <summary>
/// TextBox that paints its own selection highlight behind the text. WPF's built-in highlight is
/// drawn over the text, so any visible highlight tints it. Set SelectionOpacity to 0 to hide the
/// built-in one; this control fills the selected lines with SelectionBrush instead.
/// </summary>
public sealed class RenameTextBox : TextBox
{
    private readonly List<Rect> _selectionRects = new();
    private bool _dirty;

    public RenameTextBox()
    {
        IsVisibleChanged += (_, _) =>
        {
            LayoutUpdated -= OnLayoutUpdated;
            if (IsVisible)
            {
                _dirty = true;
                LayoutUpdated += OnLayoutUpdated;
            }
        };
        Unloaded += (_, _) => LayoutUpdated -= OnLayoutUpdated;
    }

    protected override void OnSelectionChanged(RoutedEventArgs e)
    {
        base.OnSelectionChanged(e);
        _dirty = true;
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        _dirty = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _dirty = true;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_dirty || !IsLoaded || !IsVisible)
            return;

        _dirty = false;
        RebuildSelectionRects();
        InvalidateVisual();
    }

    private void RebuildSelectionRects()
    {
        _selectionRects.Clear();
        if (SelectionLength <= 0)
            return;

        int start = SelectionStart;
        int end = start + SelectionLength;

        double lineTop = double.NaN, left = 0, right = 0, height = 0;

        void Flush()
        {
            if (!double.IsNaN(lineTop) && right > left)
                _selectionRects.Add(new Rect(left, lineTop, right - left, height));
        }

        for (int i = start; i < end; i++)
        {
            Rect leading = GetRectFromCharacterIndex(i);
            Rect trailing = GetRectFromCharacterIndex(i, true);
            if (leading.IsEmpty || trailing.IsEmpty)
                continue;

            double l = Math.Min(leading.Left, trailing.Left);
            double r = Math.Max(leading.Left, trailing.Left);

            if (double.IsNaN(lineTop) || Math.Abs(leading.Top - lineTop) > 0.5)
            {
                Flush();
                lineTop = leading.Top;
                height = leading.Height;
                left = l;
                right = r;
            }
            else
            {
                left = Math.Min(left, l);
                right = Math.Max(right, r);
            }
        }

        Flush();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var brush = SelectionBrush;
        if (brush == null)
            return;

        foreach (var rect in _selectionRects)
            drawingContext.DrawRoundedRectangle(brush, null, rect, 2, 2);
    }
}
