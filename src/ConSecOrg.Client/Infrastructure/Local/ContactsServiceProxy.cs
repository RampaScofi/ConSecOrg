using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Contacts;

namespace ConSecOrg.Client.Infrastructure.Local;

public sealed class ContactsServiceProxy : IContactsApiService
{
    private readonly ModeService _mode;
    private readonly ApiClient _api;
    private readonly LocalContactsService _local;

    public ContactsServiceProxy(ModeService mode, ApiClient api, LocalContactsService local)
    {
        _mode = mode;
        _api = api;
        _local = local;
    }

    private IContactsApiService Active => _mode.IsPersonal ? _local : _api;

    public Task<IReadOnlyList<ContactDto>> GetContactsAsync() => Active.GetContactsAsync();
    public Task<ContactDto> GetContactAsync(Guid id) => Active.GetContactAsync(id);
    public Task<Guid> CreateContactAsync(CreateContactRequestDto request) => Active.CreateContactAsync(request);
    public Task UpdateContactAsync(Guid id, UpdateContactRequestDto request) => Active.UpdateContactAsync(id, request);
    public Task DeleteContactAsync(Guid id) => Active.DeleteContactAsync(id);
}
