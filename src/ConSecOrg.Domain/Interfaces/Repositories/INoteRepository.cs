using ConSecOrg.Domain.Entities;

namespace ConSecOrg.Domain.Interfaces.Repositories;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Note>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Note>> GetExpiringAsync(DateTime before, CancellationToken ct = default);
    Task<IReadOnlyList<Note>> SearchAsync(Guid userId, string query, CancellationToken ct = default);
    Task AddAsync(Note note, CancellationToken ct = default);
    void Update(Note note);
    Task SecureDeleteAsync(Guid id, CancellationToken ct = default);
}
