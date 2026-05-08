using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class UserSettings : BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string Theme { get; private set; } = "Dark";
    public string AccentColor { get; private set; } = "#2196F3";
    public int FontSize { get; private set; } = 14;
    public string? LayoutSettingsJson { get; private set; }

    public User? User { get; private set; }

    protected UserSettings() { }

    public UserSettings(Guid id, Guid userId) : base(id)
    {
        UserId = userId;
    }

    public void UpdateTheme(string theme, string accentColor, int fontSize, string? layoutJson = null)
    {
        Theme = theme;
        AccentColor = accentColor;
        FontSize = fontSize;
        LayoutSettingsJson = layoutJson;
    }
}
