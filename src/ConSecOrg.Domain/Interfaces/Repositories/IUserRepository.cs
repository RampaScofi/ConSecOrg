using ConSecOrg.Domain.Entities;

namespace ConSecOrg.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    void Update(User user);
    void Delete(User user);
    Task<bool> ExistsAsync(string username, string email, CancellationToken ct = default);

    Task AddSettingsAsync(UserSettings settings, CancellationToken ct = default);
    Task AddSessionAsync(Session session, CancellationToken ct = default);
    Task<Session?> GetSessionByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task DeleteAllSessionsExceptAsync(Guid userId, Guid keepSessionId, CancellationToken ct = default);
}
