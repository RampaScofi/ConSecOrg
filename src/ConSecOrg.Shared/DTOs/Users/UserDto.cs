using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRoleDto Role { get; set; }
    public bool IsLocked { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AvatarBase64 { get; set; }
}
