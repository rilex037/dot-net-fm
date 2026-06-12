using System.Globalization;
using System.Windows.Data;

namespace dot_net_fm;

/// <summary>
/// Converts a file path string to an absolute URI for SvgViewbox.UriSource binding.
/// </summary>
[ValueConversion(typeof(string), typeof(Uri))]
public class StringToUriConverter : IValueConverter
{
    public static readonly StringToUriConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            return new Uri(path, UriKind.Absolute);
        }
        // Return null so SvgViewbox won't try loading anything
        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
