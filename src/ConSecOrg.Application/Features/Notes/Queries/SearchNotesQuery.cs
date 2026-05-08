using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Notes.Queries;

public record SearchNotesQuery(string Query) : IRequest<IReadOnlyList<NoteDto>>;

public class SearchNotesQueryHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<SearchNotesQuery, IReadOnlyList<NoteDto>>
{
    public async Task<IReadOnlyList<NoteDto>> Handle(SearchNotesQuery query, CancellationToken ct)
    {
        var notes = await uow.Notes.SearchAsync(currentUser.UserId, query.Query, ct);
        var encKey = currentUser.GetEncryptionKey();

        return notes.Select(n =>
        {
            string content;
            try
            {
                var plain = crypto.Decrypt(n.Content, encKey);
                content = System.Text.Encoding.UTF8.GetString(plain);
            }
            catch { content = "[Ошибка расшифровки]"; }

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
                Tags = n.Tags.Select(t => t.Name).ToList(),
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            };
        }).ToList();
    }
}
