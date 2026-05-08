using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using ConSecOrg.Shared.Pagination;
using MediatR;

namespace ConSecOrg.Application.Features.Notes.Queries;

public record GetNotesQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? CategoryId = null,
    SecurityLevelDto? SecurityLevel = null,
    string? Search = null) : IRequest<PagedResponse<NoteDto>>;

public class GetNotesQueryHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<GetNotesQuery, PagedResponse<NoteDto>>
{
    public async Task<PagedResponse<NoteDto>> Handle(GetNotesQuery query, CancellationToken ct)
    {
        var notes = await uow.Notes.GetByUserAsync(currentUser.UserId, ct);
        var encKey = currentUser.GetEncryptionKey();

        var filtered = notes.AsEnumerable();
        if (query.CategoryId.HasValue)
            filtered = filtered.Where(n => n.CategoryId == query.CategoryId);
        if (query.SecurityLevel.HasValue)
            filtered = filtered.Where(n => (int)n.SecurityLevel == (int)query.SecurityLevel);
        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(n => n.Title.Contains(query.Search, StringComparison.OrdinalIgnoreCase));

        var all = filtered.ToList();
        var total = all.Count;
        var page = all.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        var items = page.Select(n => DecryptToDto(n, encKey)).ToList();

        return new PagedResponse<NoteDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        };
    }

    private NoteDto DecryptToDto(Domain.Entities.Note n, byte[] key)
    {
        string content;
        try
        {
            var plainBytes = crypto.Decrypt(n.Content, key);
            content = System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            content = "[Ошибка расшифровки]";
        }

        return new NoteDto
        {
            Id = n.Id,
            Title = n.Title,
            Content = content,
            SecurityLevel = (SecurityLevelDto)(int)n.SecurityLevel,
            IsPinned = n.IsPinned,
            IsTemplate = n.IsTemplate,
            ExpiresAt = n.ExpiresAt,
            CategoryId = n.CategoryId,
            CategoryName = n.Category?.Name,
            Tags = n.Tags.Select(t => t.Name).ToList(),
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt
        };
    }
}
