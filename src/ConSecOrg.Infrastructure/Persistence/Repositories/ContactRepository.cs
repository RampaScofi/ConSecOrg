using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Infrastructure.Persistence.Repositories;

public sealed class ContactRepository : IContactRepository
{
    private readonly AppDbContext _db;

    public ContactRepository(AppDbContext db) => _db = db;

    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Contacts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Contact>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Contacts.Where(c => c.UserId == userId).ToListAsync(ct);

    public async Task AddAsync(Contact contact, CancellationToken ct = default)
        => await _db.Contacts.AddAsync(contact, ct);

    public void Update(Contact contact) => _db.Contacts.Update(contact);

    public void Delete(Contact contact) => _db.Contacts.Remove(contact);
}
