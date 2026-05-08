using System.Globalization;
using System.Windows.Data;

namespace ConSecOrg.Client.Themes.Converters;

public sealed class DateTimeToRelativeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;
        // Unspecified kind = our convention for UTC values returned from DB/JSON without TZ info.
        // Do NOT call ToUniversalTime() on Unspecified — it would wrongly treat the value as local time.
        var utc = dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalSeconds < 0) diff = TimeSpan.Zero;  // clock skew guard
        return diff.TotalSeconds switch
        {
            < 60  => "только что",
            < 3600 => $"{(int)diff.TotalMinutes} мин. назад",
            < 86400 => $"{(int)diff.TotalHours} ч. назад",
            < 2592000 => $"{(int)diff.TotalDays} дн. назад",
            _ => dt.ToLocalTime().ToString("dd.MM.yyyy")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
