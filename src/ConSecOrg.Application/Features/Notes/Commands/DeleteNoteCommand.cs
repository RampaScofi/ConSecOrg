using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Helpers;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using MediatR;

namespace ConSecOrg.Application.Features.Notes.Commands;

public record DeleteNoteCommand(Guid NoteId) : IRequest;

public class DeleteNoteCommandHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<DeleteNoteCommand>
{
    public async Task Handle(DeleteNoteCommand cmd, CancellationToken ct)
    {
        var note = await uow.Notes.GetByIdAsync(cmd.NoteId, ct)
            ?? throw new NotFoundException("Note", cmd.NoteId);

        if (note.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        await uow.Notes.SecureDeleteAsync(cmd.NoteId, ct);
        await AuditHelper.WriteAsync(uow, crypto, currentUser.UserId, AuditAction.NoteSecureDeleted,
            "Note", cmd.NoteId.ToString(), "Success", currentUser.IpAddress, null, ct);
        await uow.SaveChangesAsync(ct);
    }
}
