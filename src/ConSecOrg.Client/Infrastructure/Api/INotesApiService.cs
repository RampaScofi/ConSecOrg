using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Pagination;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface INotesApiService
{
    Task<PagedResponse<NoteDto>> GetNotesAsync(int page = 1, int pageSize = 20, string? search = null);
    Task<NoteDto> GetNoteAsync(Guid id);
    Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string query);
    Task<Guid> CreateNoteAsync(CreateNoteRequestDto request);
    Task UpdateNoteAsync(Guid id, UpdateNoteRequestDto request);
    Task DeleteNoteAsync(Guid id);
    Task SetDestructionTimerAsync(Guid id, DateTime expiresAt);
}
