using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ConSecOrg.Client.Controls;

public partial class DestructionTimerWidget : UserControl
{
    private DispatcherTimer? _timer;

    // ── Dependency Properties ────────────────────────────────────────────────

    public static readonly DependencyProperty ExpiresAtProperty =
        DependencyProperty.Register(nameof(ExpiresAt), typeof(DateTime?), typeof(DestructionTimerWidget),
            new PropertyMetadata(null, OnExpiresAtChanged));

    public DateTime? ExpiresAt
    {
        get => (DateTime?)GetValue(ExpiresAtProperty);
        set => SetValue(ExpiresAtProperty, value);
    }

    // Read-only computed tooltip text
    public string TooltipText =>
        ExpiresAt.HasValue ? $"Уничтожение: {ExpiresAt.Value.ToLocalTime():dd.MM.yyyy HH:mm}" : string.Empty;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public DestructionTimerWidget()
    {
        InitializeComponent();
    }

    private static void OnExpiresAtChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DestructionTimerWidget w) w.OnExpiresAtChanged();
    }

    private void OnExpiresAtChanged()
    {
        StopTimer();
        UpdateDisplay();

        var remaining = GetRemaining();
        if (remaining > TimeSpan.Zero && remaining <= TimeSpan.FromHours(25))
            StartTimer();
    }

    private void StartTimer()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            UpdateDisplay();
            if (GetRemaining() <= TimeSpan.Zero)
                StopTimer();
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private TimeSpan GetRemaining()
    {
        if (!ExpiresAt.HasValue) return TimeSpan.Zero;
        // Unspecified = our UTC-by-convention values from DB/JSON without TZ info
        var expiresUtc = ExpiresAt.Value.Kind switch
        {
            DateTimeKind.Utc => ExpiresAt.Value,
            DateTimeKind.Local => ExpiresAt.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(ExpiresAt.Value, DateTimeKind.Utc)
        };
        var remaining = expiresUtc - DateTime.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private void UpdateDisplay()
    {
        var remaining = GetRemaining();

        if (remaining <= TimeSpan.Zero)
        {
            PART_Arc.Data = null;
            PART_Text.Text = "0";
            PART_Text.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36));
            Canvas.SetTop(PART_Text, 15);
            return;
        }

        // Color based on urgency
        Color arcColor;
        if (remaining.TotalMinutes > 30)
            arcColor = Color.FromRgb(0x4C, 0xAF, 0x50);   // green
        else if (remaining.TotalMinutes > 5)
            arcColor = Color.FromRgb(0xFF, 0x98, 0x00);   // orange
        else
            arcColor = Color.FromRgb(0xF4, 0x43, 0x36);   // red

        var brush = new SolidColorBrush(arcColor);
        PART_Arc.Stroke = brush;
        PART_Text.Foreground = brush;

        // Progress: remaining / scale
        double scale = remaining.TotalHours > 1 ? 24 * 60  // minutes in 24h
                     : remaining.TotalMinutes > 10 ? 60    // minutes in 1h
                     : 10;                                  // 10 minute scale
        double totalMin = remaining.TotalMinutes;
        double progress = Math.Clamp(totalMin / scale, 0.0, 1.0);

        DrawArc(progress, arcColor);

        // Text label
        string label;
        if (remaining.TotalHours >= 1)
            label = $"{(int)remaining.TotalHours}ч";
        else if (remaining.TotalMinutes >= 1)
            label = $"{(int)remaining.TotalMinutes}м";
        else
            label = $"{(int)remaining.TotalSeconds}с";

        PART_Text.Text = label;
        // Vertically center the text in the 44px canvas (font size ~9, effective height ~12)
        Canvas.SetTop(PART_Text, (44 - 12) / 2.0 - 2);
    }

    private void DrawArc(double progress, Color color)
    {
        const double cx = 22, cy = 22, r = 16;

        if (progress >= 0.9999)
        {
            // Full circle
            PART_Arc.Data = new EllipseGeometry(new Point(cx, cy), r, r);
            return;
        }
        if (progress <= 0)
        {
            PART_Arc.Data = null;
            return;
        }

        double angle = progress * 2 * Math.PI;
        double startX = cx;
        double startY = cy - r;
        double endX = cx + r * Math.Sin(angle);
        double endY = cy - r * Math.Cos(angle);

        var figure = new PathFigure
        {
            StartPoint = new Point(startX, startY),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment(
            new Point(endX, endY),
            new Size(r, r),
            0,
            progress > 0.5,
            SweepDirection.Clockwise,
            true));

        PART_Arc.Data = new PathGeometry([figure]);
    }
}
