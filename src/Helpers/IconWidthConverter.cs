using System.Globalization;
using System.Windows.Data;

namespace DotNetFM;

public sealed class IconWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double size = value is int intSize ? intSize : 96;

        double factor = size >= 256 ? 1.15 : size >= 128 ? 1.2 : size >= 96 ? 1.3 : 1.4;
        if (parameter != null &&
            double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
            parsed > 0)
        {
            factor = parsed;
        }

        return Math.Round(size * factor);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
