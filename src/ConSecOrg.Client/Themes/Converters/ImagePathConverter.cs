using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ConSecOrg.Client.Themes.Converters;

public sealed class ImagePathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            var kind = Uri.IsWellFormedUriString(path, UriKind.Absolute)
                ? UriKind.Absolute
                : UriKind.RelativeOrAbsolute;
            return new BitmapImage(new Uri(path, kind));
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
