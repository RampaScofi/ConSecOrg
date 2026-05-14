using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

public class StringEqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object param, CultureInfo c)
    {
        var s = value?.ToString();
        var p = param?.ToString() ?? string.Empty;
        // Support "|"-separated list: "Mine|Starred"
        var match = p.Split('|').Any(part => part == s);
        return match ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type t, object param, CultureInfo c)
        => Binding.DoNothing;
}
