using ConSecOrg.Shared.DTOs.Contacts;
using ConSecOrg.Shared.DTOs.Users;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IUserSearchApiService
{
    Task<IReadOnlyList<UserSearchDto>> SearchUsersAsync(string query);
    Task<string?> GetUserAvatarAsync(Guid userId);
    Task UploadAvatarAsync(Guid userId, string? base64);
    Task SendContactRequestAsync(SendContactRequestDto request);
    Task<IReadOnlyList<ContactRequestDto>> GetPendingRequestsAsync();
    Task<IReadOnlyList<ContactRequestDto>> GetSentRequestsAsync();
    Task AcceptContactRequestAsync(Guid requestId);
    Task DeclineContactRequestAsync(Guid requestId);
}
