using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace dot_net_fm;

/// <summary>
/// Converts a discrete zoom step (0–5) into the icon pixel size
/// (24, 32, 48, 64, 128, 256) for display in the file grid.
/// </summary>
public class IconSizeStepConverter : IValueConverter
{
    public static readonly IconSizeStepConverter Instance = new();

    public static readonly int[] Sizes = { 24, 32, 48, 64, 128, 256 };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double step = value switch
        {
            double d => d,
            int i    => i,
            _        => 2,
        };

        int index = Math.Clamp((int)Math.Round(step), 0, Sizes.Length - 1);
        string? mode = parameter as string;

        double sz = Sizes[index];
        return mode switch
        {
            // Thumb offset: map step 0–5 across ~70px travel
            "offset"       => (index / (double)(Sizes.Length - 1)) * 70.0,
            // Cell: icon + proportional padding for text
            "cellWidth"    => sz + sz * 0.4,
            "cellHeight"   => sz + sz * 0.5 + 20,
            // Cell margin (as Thickness)
            "margin"       => new Thickness(Math.Max(2, sz * 0.06)),
            // Image margin below icon (as Thickness)
            "imageMargin"  => new Thickness(0, 0, 0, Math.Max(2, sz * 0.08)),
            // Max width for filename text
            "textMaxWidth" => sz + sz * 0.4,
            // Default: icon pixel size
            _              => sz,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
