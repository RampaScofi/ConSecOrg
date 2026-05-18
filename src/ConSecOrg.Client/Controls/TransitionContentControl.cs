using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ConSecOrg.Client.Controls;

/// <summary>
/// ContentControl with two-phase page transitions:
///   Phase 1 — FadeOut current content (130 ms, EaseIn)
///   Phase 2 — FadeIn + SlideFromRight new content (200 ms, EaseOut)
///
/// The shadow Content DP intercepts incoming values so the visual swap
/// happens at the right moment between the two animation phases.
/// </summary>
public sealed class TransitionContentControl : ContentControl
{
    private static readonly Duration FadeOutDur = new(TimeSpan.FromMilliseconds(130));
    private static readonly Duration FadeInDur  = new(TimeSpan.FromMilliseconds(200));
    private static readonly IEasingFunction EaseIn  = new CubicEase { EasingMode = EasingMode.EaseIn };
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

    // Shadow property — XAML Content="{Binding ...}" targets this DP.
    // base.Content (ContentControl.ContentProperty) is updated in the Completed callback
    // so the visual swap occurs at the right moment between the two animation phases.
    public new static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content), typeof(object), typeof(TransitionContentControl),
            new FrameworkPropertyMetadata(null, OnContentChanged));

    public new object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    private bool _transitioning;
    private object? _pendingContent;

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((TransitionContentControl)d).HandleContentChange(e.NewValue);

    private void HandleContentChange(object? newValue)
    {
        _pendingContent = newValue;

        // Not yet in the visual tree or already mid-transition:
        // store pending and let the Completed callback pick it up
        if (!IsLoaded)
        {
            base.Content = newValue;
            Opacity = 0;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, FadeInDur) { EasingFunction = EaseOut });
            return;
        }

        if (_transitioning) return; // Completed callback reads _pendingContent
        _transitioning = true;

        // Phase 1: fade current content out
        var fadeOut = new DoubleAnimation(Opacity, 0, FadeOutDur) { EasingFunction = EaseIn };
        fadeOut.Completed += (_, _) =>
        {
            base.Content = _pendingContent;
            _transitioning = false;

            // Phase 2: slide from right + fade in
            var translate = new TranslateTransform(36, 0);
            RenderTransform = translate;

            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(36, 0, FadeInDur) { EasingFunction = EaseOut });

            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, FadeInDur) { EasingFunction = EaseOut });
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
