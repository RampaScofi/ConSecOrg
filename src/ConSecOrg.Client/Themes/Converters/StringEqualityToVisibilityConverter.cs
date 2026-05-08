using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

public class StringEqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object param, CultureInfo c)
        => value?.ToString() == param?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type t, object param, CultureInfo c)
        => Binding.DoNothing;
}
