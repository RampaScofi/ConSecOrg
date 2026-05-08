using ConSecOrg.Shared.Enums;
using System.Globalization;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

public sealed class SecurityLevelToRussianConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is SecurityLevelDto level ? level switch
        {
            SecurityLevelDto.Public       => "Публичный",
            SecurityLevelDto.Internal     => "Внутренний",
            SecurityLevelDto.Confidential => "Конфиденциальный",
            SecurityLevelDto.Secret       => "Секретный",
            _ => value.ToString() ?? string.Empty
        } : (value?.ToString() ?? string.Empty);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
