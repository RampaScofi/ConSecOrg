using ConSecOrg.Shared.DTOs.Contacts;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IContactsApiService
{
    Task<IReadOnlyList<ContactDto>> GetContactsAsync();
    Task<ContactDto> GetContactAsync(Guid id);
    Task<Guid> CreateContactAsync(CreateContactRequestDto request);
    Task UpdateContactAsync(Guid id, UpdateContactRequestDto request);
    Task DeleteContactAsync(Guid id);
}
