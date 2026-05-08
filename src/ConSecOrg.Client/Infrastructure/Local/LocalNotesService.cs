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
        var query = _db.Notes
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

    public async Task<NoteDto> GetNoteAsync(Guid id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == _userStore.UserId)
            ?? throw new KeyNotFoundException($"Заметка {id} не найдена.");
        return ToDto(note);
    }

    public async Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string query)
    {
        var all = await _db.Notes.Where(n => n.UserId == _userStore.UserId).ToListAsync();
        return all.Select(ToDto)
            .Where(d =>
                d.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
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

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return id;
    }

    public async Task UpdateNoteAsync(Guid id, UpdateNoteRequestDto request)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == _userStore.UserId)
            ?? throw new KeyNotFoundException($"Заметка {id} не найдена.");

        var plaintext = Encoding.UTF8.GetBytes(request.Content ?? string.Empty);
        var encrypted = _crypto.Encrypt(plaintext, Key);
        Array.Clear(plaintext, 0, plaintext.Length);

        note.UpdateContent(request.Title, encrypted, (SecurityLevel)(int)request.SecurityLevel, null);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteNoteAsync(Guid id)
    {
        // Secure overwrite before deletion (best-effort; failure does not block the delete)
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE notes SET content_encrypted = CRYPT_GEN_RANDOM(DATALENGTH(content_encrypted)), " +
                "content_nonce = CRYPT_GEN_RANDOM(16), content_hmac = CRYPT_GEN_RANDOM(32) WHERE id = {0}", id);
        }
        catch { /* secure overwrite failed — proceed with delete anyway */ }

        // Use raw SQL DELETE to avoid EF change-tracker issues with the singleton DbContext
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM notes WHERE id = {0}", id);

        // Detach any tracked entity — best-effort only; EF change-tracker is not thread-safe from
        // background services, so wrap in try/catch so a concurrent-access exception does not
        // propagate and prevent the caller from knowing the DELETE succeeded.
        try
        {
            var tracked = _db.Notes.Local.FirstOrDefault(n => n.Id == id);
            if (tracked is not null)
                _db.Entry(tracked).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
        catch { }
    }

    public async Task SetDestructionTimerAsync(Guid id, DateTime expiresAt)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == _userStore.UserId)
            ?? throw new KeyNotFoundException($"Заметка {id} не найдена.");
        note.SetDestructionTimer(expiresAt.ToUniversalTime());
        await _db.SaveChangesAsync();
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
