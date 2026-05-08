using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Pagination;

namespace ConSecOrg.Client.Infrastructure.Local;

public sealed class NotesServiceProxy : INotesApiService
{
    private readonly ModeService _mode;
    private readonly ApiClient _api;
    private readonly LocalNotesService _local;

    public NotesServiceProxy(ModeService mode, ApiClient api, LocalNotesService local)
    {
        _mode = mode;
        _api = api;
        _local = local;
    }

    private INotesApiService Active => _mode.IsPersonal ? _local : _api;

    public Task<PagedResponse<NoteDto>> GetNotesAsync(int page = 1, int pageSize = 20, string? search = null)
        => Active.GetNotesAsync(page, pageSize, search);

    public Task<NoteDto> GetNoteAsync(Guid id) => Active.GetNoteAsync(id);

    public Task<IReadOnlyList<NoteDto>> SearchNotesAsync(string query) => Active.SearchNotesAsync(query);

    public Task<Guid> CreateNoteAsync(CreateNoteRequestDto request) => Active.CreateNoteAsync(request);

    public Task UpdateNoteAsync(Guid id, UpdateNoteRequestDto request) => Active.UpdateNoteAsync(id, request);

    public Task DeleteNoteAsync(Guid id) => Active.DeleteNoteAsync(id);

    public Task SetDestructionTimerAsync(Guid id, DateTime expiresAt) => Active.SetDestructionTimerAsync(id, expiresAt);
}
