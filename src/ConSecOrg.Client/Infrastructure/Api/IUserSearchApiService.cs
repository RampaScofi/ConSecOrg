using ConSecOrg.Shared.DTOs.Contacts;
using ConSecOrg.Shared.DTOs.Users;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IUserSearchApiService
{
    Task<IReadOnlyList<UserSearchDto>> SearchUsersAsync(string query);
    Task SendContactRequestAsync(SendContactRequestDto request);
    Task<IReadOnlyList<ContactRequestDto>> GetPendingRequestsAsync();
    Task<IReadOnlyList<ContactRequestDto>> GetSentRequestsAsync();
    Task AcceptContactRequestAsync(Guid requestId);
    Task DeclineContactRequestAsync(Guid requestId);
}
