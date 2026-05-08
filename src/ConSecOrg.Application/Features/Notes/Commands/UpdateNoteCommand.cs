using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Helpers;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.Enums;
using FluentValidation;
using MediatR;

namespace ConSecOrg.Application.Features.Notes.Commands;

public record UpdateNoteCommand(
    Guid NoteId,
    string Title,
    string Content,
    SecurityLevelDto SecurityLevel,
    Guid? CategoryId,
    List<string> Tags,
    bool IsPinned,
    bool IsTemplate) : IRequest;

public class UpdateNoteCommandValidator : AbstractValidator<UpdateNoteCommand>
{
    public UpdateNoteCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).NotEmpty();
    }
}

public class UpdateNoteCommandHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<UpdateNoteCommand>
{
    public async Task Handle(UpdateNoteCommand cmd, CancellationToken ct)
    {
        var note = await uow.Notes.GetByIdAsync(cmd.NoteId, ct)
            ?? throw new NotFoundException("Note", cmd.NoteId);

        if (note.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        var encKey = currentUser.GetEncryptionKey();
        var encrypted = crypto.Encrypt(System.Text.Encoding.UTF8.GetBytes(cmd.Content), encKey);
        var level = (SecurityLevel)(int)cmd.SecurityLevel;

        note.UpdateContent(cmd.Title, encrypted, level, cmd.CategoryId);
        if (cmd.IsPinned) note.Pin(); else note.Unpin();

        uow.Notes.Update(note);
        await AuditHelper.WriteAsync(uow, crypto, currentUser.UserId, AuditAction.NoteUpdated,
            "Note", cmd.NoteId.ToString(), "Success", currentUser.IpAddress, $"Title: {cmd.Title}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
