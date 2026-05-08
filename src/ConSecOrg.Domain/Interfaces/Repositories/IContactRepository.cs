using ConSecOrg.Domain.Entities;

namespace ConSecOrg.Domain.Interfaces.Repositories;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Contact>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Contact contact, CancellationToken ct = default);
    void Update(Contact contact);
    void Delete(Contact contact);
}
