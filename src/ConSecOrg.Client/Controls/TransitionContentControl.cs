using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ConSecOrg.Client.Controls;

/// <summary>
/// ContentControl that fades in (0→1, 180ms) whenever Content changes.
/// </summary>
public sealed class TransitionContentControl : ContentControl
{
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(180));

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (newContent is null) return;

        Opacity = 0;
        var anim = new DoubleAnimation(0, 1, FadeDuration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }
}
