using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using MediatR;

namespace ConSecOrg.Application.Features.Notes.Commands;

public record SetDestructionTimerCommand(Guid NoteId, DateTime ExpiresAt) : IRequest;

public class SetDestructionTimerCommandHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<SetDestructionTimerCommand>
{
    public async Task Handle(SetDestructionTimerCommand cmd, CancellationToken ct)
    {
        var note = await uow.Notes.GetByIdAsync(cmd.NoteId, ct)
            ?? throw new NotFoundException("Note", cmd.NoteId);

        if (note.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        note.SetDestructionTimer(cmd.ExpiresAt);
        uow.Notes.Update(note);
        await uow.SaveChangesAsync(ct);
    }
}
