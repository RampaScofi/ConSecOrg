using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ConSecOrg.Client.Themes.Converters;

/// <summary>Converts a hex color string to a SolidColorBrush with 30% alpha — used for column count badges.</summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class HexToAlpha30Converter : IValueConverter
{
    public static readonly HexToAlpha30Converter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(Color.FromArgb(0x4D, c.R, c.G, c.B));
            }
            catch { }
        }
        return new SolidColorBrush(Color.FromArgb(0x4D, 96, 125, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
