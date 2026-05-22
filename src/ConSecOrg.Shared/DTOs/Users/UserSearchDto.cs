namespace ConSecOrg.Shared.DTOs.Users;

public class UserSearchDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarBase64 { get; set; }
}
