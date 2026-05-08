using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Notes.Queries;

public record GetNoteByIdQuery(Guid NoteId) : IRequest<NoteDto>;

public class GetNoteByIdQueryHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<GetNoteByIdQuery, NoteDto>
{
    public async Task<NoteDto> Handle(GetNoteByIdQuery query, CancellationToken ct)
    {
        var note = await uow.Notes.GetByIdAsync(query.NoteId, ct)
            ?? throw new NotFoundException("Note", query.NoteId);

        if (note.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        var encKey = currentUser.GetEncryptionKey();
        var plainBytes = crypto.Decrypt(note.Content, encKey);
        var content = System.Text.Encoding.UTF8.GetString(plainBytes);

        return new NoteDto
        {
            Id = note.Id,
            Title = note.Title,
            Content = content,
            SecurityLevel = (SecurityLevelDto)(int)note.SecurityLevel,
            IsPinned = note.IsPinned,
            IsTemplate = note.IsTemplate,
            ExpiresAt = note.ExpiresAt,
            CategoryId = note.CategoryId,
            CategoryName = note.Category?.Name,
            Tags = note.Tags.Select(t => t.Name).ToList(),
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
    }
}
