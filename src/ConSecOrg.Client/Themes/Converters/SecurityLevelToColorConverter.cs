using ConSecOrg.Shared.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ConSecOrg.Client.Themes.Converters;

public sealed class SecurityLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SecurityLevelDto level)
        {
            return level switch
            {
                SecurityLevelDto.Public       => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                SecurityLevelDto.Internal     => new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
                SecurityLevelDto.Confidential => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                SecurityLevelDto.Secret       => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
