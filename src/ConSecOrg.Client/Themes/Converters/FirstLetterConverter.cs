using System.Globalization;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

public class FirstLetterConverter : IValueConverter
{
    public object Convert(object value, Type t, object param, CultureInfo c)
        => value is string s && s.Length > 0 ? s[0].ToString().ToUpper() : "?";
    public object ConvertBack(object value, Type t, object param, CultureInfo c)
        => Binding.DoNothing;
}
