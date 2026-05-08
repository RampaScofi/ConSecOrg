using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApplication = System.Windows.Application;

namespace ConSecOrg.Client.Themes;

public sealed class ThemeEngine
{
    private static ThemeEngine? _instance;
    public static ThemeEngine Instance => _instance ??= new ThemeEngine();

    private ResourceDictionary _dynamicTheme = new();
    private string _currentTheme = "Dark";
    private Color _accentColor = (Color)ColorConverter.ConvertFromString("#6C63FF");
    private Color _secondaryAccentColor = (Color)ColorConverter.ConvertFromString("#00BCD4");
    private int _fontSize = 14;
    private string? _backgroundImagePath;
    private double _backgroundOverlayOpacity = 0.88;

    public string CurrentTheme => _currentTheme;
    public Color AccentColor => _accentColor;
    public Color SecondaryAccentColor => _secondaryAccentColor;
    public int FontSize => _fontSize;
    public string? BackgroundImagePath => _backgroundImagePath;
    public double BackgroundOverlayOpacity => _backgroundOverlayOpacity;

    public event Action? ThemeChanged;

    public void Initialize(WpfApplication app)
    {
        app.Resources.MergedDictionaries.Add(_dynamicTheme);
        Apply(_currentTheme, _accentColor, _secondaryAccentColor, _fontSize, _backgroundImagePath, _backgroundOverlayOpacity);
    }

    public void Apply(string theme, Color accent, Color secondaryAccent, int fontSize,
                      string? bgImagePath = null, double overlayOpacity = 0.88)
    {
        _currentTheme = theme;
        _accentColor = accent;
        _secondaryAccentColor = secondaryAccent;
        _fontSize = fontSize;
        _backgroundImagePath = bgImagePath;
        _backgroundOverlayOpacity = Math.Clamp(overlayOpacity, 0.0, 1.0);

        _dynamicTheme.Clear();

        var isDark = theme != "Light";

        // ── Palette ────────────────────────────────────────────────────────────
        Color bg1, bg2, sidebar, card, navActive, fg, fgSec, border, inputBg;

        if (isDark)
        {
            // Deep dark theme: Notion-like rich dark blues
            bg1      = Color.FromRgb(0x12, 0x13, 0x1E); // #12131E — main bg
            bg2      = Color.FromRgb(0x18, 0x19, 0x27); // #181927 — secondary bg
            sidebar  = Color.FromRgb(0x0E, 0x0F, 0x1A); // #0E0F1A — sidebar
            card     = Color.FromRgb(0x1E, 0x1F, 0x2E); // #1E1F2E — card
            navActive= Color.FromRgb(0x26, 0x27, 0x3A); // #26273A — nav hover
            fg       = Color.FromRgb(0xED, 0xEF, 0xF8); // #EDEFF8 — primary text (near white)
            fgSec    = Color.FromRgb(0x8B, 0x90, 0xB5); // #8B90B5 — secondary text
            border   = Color.FromRgb(0x2C, 0x2E, 0x45); // #2C2E45 — borders
            inputBg  = Color.FromRgb(0x22, 0x23, 0x34); // #222334 — input fields
        }
        else
        {
            // Clean light theme: soft whites with blue tints
            bg1      = Color.FromRgb(0xF4, 0xF5, 0xFB); // #F4F5FB
            bg2      = Color.FromRgb(0xFF, 0xFF, 0xFF); // white
            sidebar  = Color.FromRgb(0xFF, 0xFF, 0xFF); // white sidebar
            card     = Color.FromRgb(0xFF, 0xFF, 0xFF);
            navActive= Color.FromRgb(0xEE, 0xEF, 0xFF);
            fg       = Color.FromRgb(0x1A, 0x1C, 0x30); // deep dark text
            fgSec    = Color.FromRgb(0x5C, 0x62, 0x82);
            border   = Color.FromRgb(0xE2, 0xE4, 0xF0);
            inputBg  = Color.FromRgb(0xF7, 0xF8, 0xFF);
        }

        var accentDark  = DarkenColor(accent, 0.15f);
        var accentLight = LightenColor(accent, 0.15f);
        var accentAlpha = Color.FromArgb(0x22, accent.R, accent.G, accent.B); // 13% alpha
        var accentAlpha2= Color.FromArgb(0x3A, accent.R, accent.G, accent.B); // 23% alpha

        // Determine nav active text: always white in dark, dark in light
        var navActiveFg = isDark
            ? Color.FromRgb(0xFF, 0xFF, 0xFF)
            : fg;

        // ── Apply brushes ──────────────────────────────────────────────────────
        _dynamicTheme["PrimaryBackground"]      = new SolidColorBrush(bg1);
        _dynamicTheme["SecondaryBackground"]    = new SolidColorBrush(bg2);
        _dynamicTheme["SidebarBackground"]      = new SolidColorBrush(sidebar);
        _dynamicTheme["CardBackground"]         = new SolidColorBrush(card);
        _dynamicTheme["NavActiveBackground"]    = new SolidColorBrush(navActive);
        _dynamicTheme["NavActiveForeground"]    = new SolidColorBrush(navActiveFg);
        _dynamicTheme["PrimaryForeground"]      = new SolidColorBrush(fg);
        _dynamicTheme["SecondaryForeground"]    = new SolidColorBrush(fgSec);
        _dynamicTheme["BorderBrush"]            = new SolidColorBrush(border);
        _dynamicTheme["AccentBrush"]            = new SolidColorBrush(accent);
        _dynamicTheme["AccentDarkBrush"]        = new SolidColorBrush(accentDark);
        _dynamicTheme["AccentLightBrush"]       = new SolidColorBrush(accentLight);
        _dynamicTheme["AccentAlphaBrush"]       = new SolidColorBrush(accentAlpha);
        _dynamicTheme["AccentAlpha2Brush"]      = new SolidColorBrush(accentAlpha2);
        _dynamicTheme["AccentForeground"]       = new SolidColorBrush(Colors.White);
        _dynamicTheme["AccentColor"]            = accent;
        _dynamicTheme["BaseFontSize"]           = (double)fontSize;

        // Secondary accent (configurable independently)
        var secDark  = DarkenColor(secondaryAccent, 0.15f);
        var secLight = LightenColor(secondaryAccent, 0.15f);
        var secAlpha = Color.FromArgb(0x33, secondaryAccent.R, secondaryAccent.G, secondaryAccent.B);
        var secFgColor = IsLightColor(secondaryAccent) ? Color.FromRgb(0x1A, 0x1C, 0x30) : Colors.White;
        _dynamicTheme["SecondaryAccentBrush"]      = new SolidColorBrush(secondaryAccent);
        _dynamicTheme["SecondaryAccentDarkBrush"]  = new SolidColorBrush(secDark);
        _dynamicTheme["SecondaryAccentLightBrush"] = new SolidColorBrush(secLight);
        _dynamicTheme["SecondaryAccentAlphaBrush"] = new SolidColorBrush(secAlpha);
        _dynamicTheme["SecondaryAccentForeground"] = new SolidColorBrush(secFgColor);
        _dynamicTheme["InputBackground"]        = new SolidColorBrush(inputBg);

        // Priority badge colours
        _dynamicTheme["PriorityLow"]      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
        _dynamicTheme["PriorityNormal"]   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
        _dynamicTheme["PriorityHigh"]     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
        _dynamicTheme["PriorityCritical"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));

        // ── MaterialDesign overrides ───────────────────────────────────────────
        _dynamicTheme["MaterialDesignBody"]                       = new SolidColorBrush(fg);
        _dynamicTheme["MaterialDesignBodyLight"]                  = new SolidColorBrush(fgSec);
        _dynamicTheme["MaterialDesignPaper"]                      = new SolidColorBrush(bg2);
        _dynamicTheme["MaterialDesignBackground"]                 = new SolidColorBrush(bg1);
        _dynamicTheme["MaterialDesignCardBackground"]             = new SolidColorBrush(card);
        _dynamicTheme["MaterialDesignToolBarBackground"]          = new SolidColorBrush(sidebar);
        _dynamicTheme["MaterialDesignDivider"]                    = new SolidColorBrush(border);
        _dynamicTheme["MaterialDesignFlatButtonClick"]            = new SolidColorBrush(navActive);
        _dynamicTheme["MaterialDesignTextFieldBoxBackground"]     = new SolidColorBrush(inputBg);
        _dynamicTheme["MaterialDesignTextAreaBorder"]             = new SolidColorBrush(border);
        _dynamicTheme["MaterialDesignTextBoxBorder"]              = new SolidColorBrush(border);

        // Propagate accent to MaterialDesign primary/secondary hue brushes
        // so raised/flat buttons, checkboxes, sliders, progress bars, etc. use chosen accent
        var accentFgColor = IsLightColor(accent) ? Color.FromRgb(0x1A, 0x1C, 0x30) : Colors.White;
        var accentFgBrush = new SolidColorBrush(accentFgColor);
        var accentBrush   = new SolidColorBrush(accent);
        var accentDarkBrush2  = new SolidColorBrush(accentDark);
        var accentLightBrush2 = new SolidColorBrush(accentLight);

        _dynamicTheme["PrimaryHueLightBrush"]              = accentLightBrush2;
        _dynamicTheme["PrimaryHueLightForegroundBrush"]    = accentFgBrush;
        _dynamicTheme["PrimaryHueMidBrush"]                = accentBrush;
        _dynamicTheme["PrimaryHueMidForegroundBrush"]      = accentFgBrush;
        _dynamicTheme["PrimaryHueDarkBrush"]               = accentDarkBrush2;
        _dynamicTheme["PrimaryHueDarkForegroundBrush"]     = accentFgBrush;
        // Secondary hue uses the independently chosen secondary accent colour
        var secMidBrush   = new SolidColorBrush(secondaryAccent);
        var secLightBrush = new SolidColorBrush(LightenColor(secondaryAccent, 0.15f));
        var secDarkBrush2 = new SolidColorBrush(DarkenColor(secondaryAccent, 0.15f));
        var secFgBrush    = new SolidColorBrush(IsLightColor(secondaryAccent) ? Color.FromRgb(0x1A, 0x1C, 0x30) : Colors.White);
        _dynamicTheme["SecondaryHueMidBrush"]              = secMidBrush;
        _dynamicTheme["SecondaryHueMidForegroundBrush"]    = secFgBrush;
        _dynamicTheme["SecondaryHueLightBrush"]            = secLightBrush;
        _dynamicTheme["SecondaryHueLightForegroundBrush"]  = secFgBrush;
        _dynamicTheme["SecondaryHueDarkBrush"]             = secDarkBrush2;
        _dynamicTheme["SecondaryHueDarkForegroundBrush"]   = secFgBrush;

        // ── Background image ───────────────────────────────────────────────────
        bool hasImage = !string.IsNullOrEmpty(bgImagePath) && File.Exists(bgImagePath);
        if (hasImage)
        {
            try
            {
                var imgSource = new BitmapImage(new Uri(bgImagePath!, UriKind.Absolute));
                var imgBrush  = new ImageBrush(imgSource) { Stretch = Stretch.UniformToFill };
                _dynamicTheme["AppBackgroundBrush"] = imgBrush;
                var overlayColor = Color.FromArgb(
                    (byte)(overlayOpacity * 255), bg1.R, bg1.G, bg1.B);
                _dynamicTheme["AppOverlayBrush"] = new SolidColorBrush(overlayColor);

                // Views become transparent so the image shows through
                _dynamicTheme["PrimaryBackground"] = new SolidColorBrush(Colors.Transparent);

                // Semi-transparent card / secondary for readability over image
                var cardAlpha    = Color.FromArgb(0xD8, card.R,    card.G,    card.B);    // 85%
                var secondaryAlpha = Color.FromArgb(0xCC, bg2.R,   bg2.G,    bg2.B);      // 80%
                var sidebarAlpha = Color.FromArgb(0xE6, sidebar.R, sidebar.G, sidebar.B); // 90%
                _dynamicTheme["CardBackground"]      = new SolidColorBrush(cardAlpha);
                _dynamicTheme["SecondaryBackground"] = new SolidColorBrush(secondaryAlpha);
                _dynamicTheme["SidebarBackground"]   = new SolidColorBrush(sidebarAlpha);
            }
            catch
            {
                hasImage = false;
            }
        }

        if (!hasImage)
        {
            _dynamicTheme["AppBackgroundBrush"] = new SolidColorBrush(bg1);
            _dynamicTheme["AppOverlayBrush"]    = new SolidColorBrush(Colors.Transparent);
        }

        // ── MaterialDesign PaletteHelper: override MD internal theme colours ─────
        // This is the authoritative way to change MD5 button/checkbox/FAB colours.
        try
        {
            var ph = new PaletteHelper();
            var mdTheme = ph.GetTheme();
            mdTheme.SetPrimaryColor(accent);
            mdTheme.SetSecondaryColor(secondaryAccent);
            mdTheme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            ph.SetTheme(mdTheme);
        }
        catch { /* PaletteHelper unavailable (e.g. in tests) — ignore */ }

        ThemeChanged?.Invoke();
    }

    public void ApplyFromSettings(string theme, string accentHex, int fontSize,
                                   string? bgImagePath = null, double overlayOpacity = 0.88,
                                   string secondaryAccentHex = "#00BCD4")
    {
        Color accent;
        try { accent = (Color)ColorConverter.ConvertFromString(accentHex); }
        catch { accent = (Color)ColorConverter.ConvertFromString("#6C63FF"); }
        Color secondary;
        try { secondary = (Color)ColorConverter.ConvertFromString(secondaryAccentHex); }
        catch { secondary = (Color)ColorConverter.ConvertFromString("#00BCD4"); }
        Apply(theme, accent, secondary, fontSize, bgImagePath, overlayOpacity);
    }

    // ── Colour helpers ─────────────────────────────────────────────────────────
    private static bool IsLightColor(Color c)
    {
        // Relative luminance: perceived brightness > 0.5 → light
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        return 0.299 * r + 0.587 * g + 0.114 * b > 0.55;
    }

    private static Color DarkenColor(Color c, float amount)
    {
        ColorToHsl(c, out float h, out float s, out float l);
        l = Math.Max(0, l - amount);
        return HslToColor(h, s, l);
    }

    private static Color LightenColor(Color c, float amount)
    {
        ColorToHsl(c, out float h, out float s, out float l);
        l = Math.Min(1, l + amount);
        return HslToColor(h, s, l);
    }

    private static void ColorToHsl(Color c, out float h, out float s, out float l)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2f;
        if (max == min) { h = s = 0; return; }
        float d = max - min;
        s = l > 0.5f ? d / (2 - max - min) : d / (max + min);
        if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6f;
        else if (max == g) h = ((b - r) / d + 2) / 6f;
        else h = ((r - g) / d + 4) / 6f;
    }

    private static Color HslToColor(float h, float s, float l)
    {
        if (s == 0) { byte v = (byte)(l * 255); return Color.FromRgb(v, v, v); }
        float q = l < 0.5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        return Color.FromRgb(
            (byte)(Hue(p, q, h + 1f / 3) * 255),
            (byte)(Hue(p, q, h) * 255),
            (byte)(Hue(p, q, h - 1f / 3) * 255));
    }

    private static float Hue(float p, float q, float t)
    {
        if (t < 0) t += 1; if (t > 1) t -= 1;
        if (t < 1f / 6) return p + (q - p) * 6 * t;
        if (t < 0.5f) return q;
        if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
        return p;
    }
}
