using System.Globalization;
using System.Windows.Data;

namespace DotNetFM;

[ValueConversion(typeof(string), typeof(Uri))]
public class StringToUriConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            return new Uri(path, UriKind.Absolute);
        }
        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
