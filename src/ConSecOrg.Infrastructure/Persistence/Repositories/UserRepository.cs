using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.Include(u => u.Role).Include(u => u.Settings)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => _db.Users.Include(u => u.Role).Include(u => u.Settings)
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
        => await _db.Users.Include(u => u.Role).ToListAsync(ct);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await _db.Users.Where(u => idList.Contains(u.Id)).ToListAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    public void Update(User user) => _db.Users.Update(user);

    public void Delete(User user) => _db.Users.Remove(user);

    public Task<bool> ExistsAsync(string username, string email, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.Username == username || u.Email == email, ct);

    public async Task AddSettingsAsync(UserSettings settings, CancellationToken ct = default)
        => await _db.UserSettings.AddAsync(settings, ct);

    public async Task AddSessionAsync(Session session, CancellationToken ct = default)
        => await _db.Sessions.AddAsync(session, ct);

    public Task<Session?> GetSessionByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
        => _db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == tokenHash && s.ExpiresAt > DateTime.UtcNow, ct);

    public async Task<IReadOnlyList<Session>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default)
        => await _db.Sessions
            .Where(s => s.UserId == userId && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!(_db.Database.ProviderName?.Contains("InMemory") == true))
        {
            await _db.Sessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync(ct);
        }
        else
        {
            var session = await _db.Sessions.FindAsync([sessionId], ct);
            if (session is not null) _db.Sessions.Remove(session);
        }
    }

    public async Task DeleteAllSessionsExceptAsync(Guid userId, Guid keepSessionId, CancellationToken ct = default)
    {
        if (!(_db.Database.ProviderName?.Contains("InMemory") == true))
        {
            await _db.Sessions
                .Where(s => s.UserId == userId && s.Id != keepSessionId)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            var toRemove = await _db.Sessions
                .Where(s => s.UserId == userId && s.Id != keepSessionId)
                .ToListAsync(ct);
            _db.Sessions.RemoveRange(toRemove);
        }
    }
}
