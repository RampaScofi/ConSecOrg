using ConSecOrg.Shared.DTOs.Users;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IUsersManagementApiService
{
    Task<IReadOnlyList<UserDto>> GetAllUsersAsync();
    Task AssignRoleAsync(Guid userId, Guid roleId);
    Task LockUserAsync(Guid userId);
    Task UnlockUserAsync(Guid userId);
}
