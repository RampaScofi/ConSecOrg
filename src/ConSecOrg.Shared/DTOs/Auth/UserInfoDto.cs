using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Auth;

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRoleDto Role { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public bool IsLocked { get; set; }
}
