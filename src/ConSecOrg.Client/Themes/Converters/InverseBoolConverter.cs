using System.Globalization;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? false : (object)true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is true ? false : (object)true;
}
