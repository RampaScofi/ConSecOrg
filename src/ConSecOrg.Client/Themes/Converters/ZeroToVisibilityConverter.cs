using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

/// <summary>Converts int count: 0 → Visible (show "empty state"), >0 → Collapsed.</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
