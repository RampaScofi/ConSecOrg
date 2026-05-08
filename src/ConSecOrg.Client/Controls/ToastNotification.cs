using ConSecOrg.Client.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SysWin = System.Windows;

namespace ConSecOrg.Client.Controls;

/// <summary>
/// Creates and auto-dismisses a toast notification Border.
/// </summary>
public static class ToastNotificationHelper
{
    private static readonly Dictionary<NotificationLevel, (string bg, string fg, string icon)> _styles = new()
    {
        [NotificationLevel.Success] = ("#388E3C", "#FFFFFF", "✓"),
        [NotificationLevel.Error]   = ("#C62828", "#FFFFFF", "✗"),
        [NotificationLevel.Warning] = ("#E65100", "#FFFFFF", "⚠"),
        [NotificationLevel.Info]    = ("#1565C0", "#FFFFFF", "ℹ"),
    };

    public static void Show(ItemsControl overlay, string title, string message, NotificationLevel level)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() =>
        {
            var (bg, fg, icon) = _styles[level];

            var toast = BuildToast(title, message, bg, fg, icon);
            overlay.Items.Add(toast);

            // Fade out after 3.5 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3.5)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(300)));
                fade.Completed += (_, _) => overlay.Items.Remove(toast);
                toast.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            timer.Start();
        });
    }

    private static Border BuildToast(string title, string message, string bgHex, string fgHex, string icon)
    {
        var bg = (Brush)new BrushConverter().ConvertFromString(bgHex)!;
        var fg = (Brush)new BrushConverter().ConvertFromString(fgHex)!;

        var iconBlock = new TextBlock
        {
            Text = icon,
            FontSize = 18,
            Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap
        };

        var msgBlock = new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = fg,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.9
        };

        var textStack = new StackPanel();
        textStack.Children.Add(titleBlock);
        if (!string.IsNullOrEmpty(message))
            textStack.Children.Add(msgBlock);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(iconBlock, 0);
        Grid.SetColumn(textStack, 1);
        row.Children.Add(iconBlock);
        row.Children.Add(textStack);

        var border = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 8),
            MinWidth = 280,
            MaxWidth = 380,
            Child = row,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = 0.25,
                BlurRadius = 12,
                ShadowDepth = 4
            }
        };

        // Slide in + fade in
        border.Opacity = 0;
        border.RenderTransform = new TranslateTransform(40, 0);
        border.RenderTransformOrigin = new Point(0.5, 0.5);

        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(250)));
        var slideIn = new DoubleAnimation(40, 0, new Duration(TimeSpan.FromMilliseconds(250)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        border.Loaded += (_, _) =>
        {
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ((TranslateTransform)border.RenderTransform).BeginAnimation(TranslateTransform.XProperty, slideIn);
        };

        return border;
    }
}
