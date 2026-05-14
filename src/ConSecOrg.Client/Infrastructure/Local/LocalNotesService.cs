using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Crypto;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using ConSecOrg.Shared.Pagination;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ConSecOrg.Client.Infrastructure.Local;

public sealed class LocalNotesService : INotesApiService
{
    private readonly PersonalDbContext _db;
    private readonly GostCryptoService _crypto = new();
    private readonly SessionService _session;
    private readonly LocalUserStore _userStore;
    // Serialise all DB access: PersonalDbContext is a Singleton shared between the UI thread
    // and the background DestructionTimerService — EF DbContext is not thread-safe.
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public LocalNotesService(PersonalDbContext db, SessionService session, LocalUserStore userStore)
    {
        _db = db;
        _session = session;
        _userStore = userStore;
    }

    private byte[] Key => _session.GetEncryptionKey()
        ?? throw new InvalidOperationException("Ключ шифрования недоступен. Введите PIN.");

    public async Task<PagedResponse<NoteDto>> GetNotesAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            // AsNoTracking: background thread must not pollute the shared change-tracker
            var query = _db.Notes
                .AsNoTracking()
                .Where(n => n.UserId == _userStore.UserId)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.UpdatedAt);

            var total = await query.CountAsync();
            var notes = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var dtos = notes.Select(ToDto).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                dtos = dtos.Where(d =>
                    d.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.Content.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return new PagedResponse<NoteDto>
            {
                Items = dtos,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }
        finally { _dbLock.Release(); }
    }

    public async Task<NoteDto> GetNoteAsync(Guid id)
    {
        await _dbLock.WaitAsync();
        try
        {
            var note = await _db.Notes.AsNoTracking()
                           .FirstOrDefaultAsync(n => n.Id == id && n.UserId == _userStore.UserId)
                       ?? throw new KeyNotFoundException($"Заметка {id} не найдена.");
            return ToDto(note);
        }
        finally { _dbLock.Release(); }
    }

    public async Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string query)
    {
        await _dbLock.WaitAsync();
        try
        {
            var all = await _db.Notes.AsNoTracking()
                          .Where(n => n.UserId == _userStore.UserId).ToListAsync();
            return all.Select(ToDto)
                .Where(d =>
                    d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    d.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        finally { _dbLock.Release(); }
    }

    public async Task<Guid> CreateNoteAsync(CreateNoteRequestDto request)
    {
        var id = Guid.NewGuid();
        var plaintext = Encoding.UTF8.GetBytes(request.Content ?? string.Empty);
        var encrypted = _crypto.Encrypt(plaintext, Key);
        Array.Clear(plaintext, 0, plaintext.Length);

        var note = new Note(id, _userStore.UserId, request.Title, encrypted,
            (SecurityLevel)(int)request.SecurityLevel);

        if (request.IsPinned) note.Pin();
        if (request.ExpiresAt.HasValue)
        {
            var utc = request.ExpiresAt.Value.Kind switch
            {
                DateTimeKind.Utc => request.ExpiresAt.Value,
                DateTimeKind.Local => request.ExpiresAt.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(request.ExpiresAt.Value, DateTimeKind.Local).ToUniversalTime()
            };
            note.SetDestructionTimer(utc);
        }

        await _dbLock.WaitAsync();
        try
        {
            _db.Notes.Add(note);
            await _db.SaveChangesAsync();
        }
        finally { _dbLock.Release(); }
        return id;
    }

    public async Task UpdateNoteAsync(Guid id, UpdateNoteRequestDto request)
    {
        var plaintext = Encoding.UTF8.GetBytes(request.Content ?? string.Empty);
        var encrypted = _crypto.Encrypt(plaintext, Key);
        Array.Clear(plaintext, 0, plaintext.Length);

        await _dbLock.WaitAsync();
        try
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == _userStore.UserId)
                ?? throw new KeyNotFoundException($"Заметка {id} не найдена.");
            note.UpdateContent(request.Title, encrypted, (SecurityLevel)(int)request.SecurityLevel, null);
            await _db.SaveChangesAsync();
        }
        finally { _dbLock.Release(); }
    }

    public async Task DeleteNoteAsync(Guid id)
    {
        await _dbLock.WaitAsync();
        try
        {
            // FindAsync checks the local cache first, then queries the DB
            var note = await _db.Notes.FindAsync(id);
            if (note is null) return; // Already deleted — nothing to do

            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();
        }
        finally { _dbLock.Release(); }
    }

    public async Task SetDestructionTimerAsync(Guid id, DateTime expiresAt)
    {
        await _dbLock.WaitAsync();
        try
        {
            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == _userStore.UserId)
                ?? throw new KeyNotFoundException($"Заметка {id} не найдена.");
            note.SetDestructionTimer(expiresAt.ToUniversalTime());
            await _db.SaveChangesAsync();
        }
        finally { _dbLock.Release(); }
    }

    private NoteDto ToDto(Note n)
    {
        string content = string.Empty;
        try
        {
            var plaintext = _crypto.Decrypt(n.Content, Key);
            content = Encoding.UTF8.GetString(plaintext);
            Array.Clear(plaintext, 0, plaintext.Length);
        }
        catch { /* HMAC failure or decryption error — return empty content */ }

        // EF Core возвращает DateTime с Kind=Unspecified — принудительно метим как UTC,
        // т.к. в БД сохранялось через ToUniversalTime()
        DateTime? expiresUtc = n.ExpiresAt.HasValue
            ? DateTime.SpecifyKind(n.ExpiresAt.Value, DateTimeKind.Utc)
            : null;

        return new NoteDto
        {
            Id = n.Id,
            Title = n.Title,
            Content = content,
            SecurityLevel = (SecurityLevelDto)(int)n.SecurityLevel,
            IsPinned = n.IsPinned,
            IsTemplate = n.IsTemplate,
            ExpiresAt = expiresUtc,
            CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(n.UpdatedAt, DateTimeKind.Utc)
        };
    }
}
