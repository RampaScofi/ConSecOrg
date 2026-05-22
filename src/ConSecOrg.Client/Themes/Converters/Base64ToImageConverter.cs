using ConSecOrg.Client.Services;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ConSecOrg.Client.Themes.Converters;

public sealed class Base64ToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => AvatarCacheService.Base64ToImage(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
