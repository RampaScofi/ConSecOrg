using System.Globalization;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

public class StringEqualityConverter : IValueConverter
{
    public object Convert(object value, Type t, object param, CultureInfo c)
        => value?.ToString() == param?.ToString();
    public object ConvertBack(object value, Type t, object param, CultureInfo c)
        => Binding.DoNothing;
}
