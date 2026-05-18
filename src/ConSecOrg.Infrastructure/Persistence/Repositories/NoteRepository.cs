using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Infrastructure.Persistence.Repositories;

public sealed class NoteRepository : INoteRepository
{
    private readonly AppDbContext _db;

    public NoteRepository(AppDbContext db) => _db = db;

    public Task<Note?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Notes.Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Note>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Notes.Include(n => n.Tags).Include(n => n.Category)
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Note>> GetExpiringAsync(DateTime before, CancellationToken ct = default)
        => await _db.Notes
            .Where(n => n.ExpiresAt != null && n.ExpiresAt <= before)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Note>> SearchAsync(Guid userId, string query, CancellationToken ct = default)
        => await _db.Notes
            .Where(n => n.UserId == userId && n.Title.Contains(query))
            .ToListAsync(ct);

    public async Task AddAsync(Note note, CancellationToken ct = default)
        => await _db.Notes.AddAsync(note, ct);

    public void Update(Note note) => _db.Notes.Update(note);

    public async Task SecureDeleteAsync(Guid id, CancellationToken ct = default)
    {
        // InMemory provider (used in integration tests) doesn't support raw SQL or bulk deletes.
        var isRelational = !(_db.Database.ProviderName?.Contains("InMemory") == true);

        if (isRelational)
        {
            // Overwrite encrypted fields with random data before deletion (SQL Server only)
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE notes SET content_encrypted = CRYPT_GEN_RANDOM(CAST(DATALENGTH(content_encrypted) AS INT)), " +
                "content_nonce = CRYPT_GEN_RANDOM(16), content_hmac = CRYPT_GEN_RANDOM(32) " +
                "WHERE Id = {0}", [id], ct);
            await _db.Notes.Where(n => n.Id == id).ExecuteDeleteAsync(ct);
        }
        else
        {
            var note = await _db.Notes.FindAsync([id], ct);
            if (note is not null) _db.Notes.Remove(note);
        }
    }
}
