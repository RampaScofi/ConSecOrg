namespace ConSecOrg.Shared.DTOs.Users;

public class UserSettingsDto
{
    public Guid UserId { get; set; }
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#2196F3";
    public int FontSize { get; set; } = 14;
    public string? LayoutSettings { get; set; }
}
