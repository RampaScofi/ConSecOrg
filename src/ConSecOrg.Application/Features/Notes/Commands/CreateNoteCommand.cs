using ConSecOrg.Application.Common.Helpers;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.Enums;
using FluentValidation;
using MediatR;

namespace ConSecOrg.Application.Features.Notes.Commands;

public record CreateNoteCommand(
    string Title,
    string Content,
    SecurityLevelDto SecurityLevel,
    Guid? CategoryId,
    List<string> Tags,
    bool IsTemplate,
    bool IsPinned,
    DateTime? ExpiresAt) : IRequest<Guid>;

public class CreateNoteCommandValidator : AbstractValidator<CreateNoteCommand>
{
    public CreateNoteCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
    }
}

public class CreateNoteCommandHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<CreateNoteCommand, Guid>
{
    public async Task<Guid> Handle(CreateNoteCommand cmd, CancellationToken ct)
    {
        var encKey = currentUser.GetEncryptionKey();
        var plaintext = System.Text.Encoding.UTF8.GetBytes(cmd.Content);
        var encrypted = crypto.Encrypt(plaintext, encKey);

        var level = (SecurityLevel)(int)cmd.SecurityLevel;
        var id = Guid.NewGuid();
        var note = new Note(id, currentUser.UserId, cmd.Title, encrypted, level, cmd.CategoryId, cmd.IsTemplate);

        if (cmd.IsPinned) note.Pin();
        if (cmd.ExpiresAt.HasValue) note.SetDestructionTimer(cmd.ExpiresAt.Value);

        await uow.Notes.AddAsync(note, ct);
        await AuditHelper.WriteAsync(uow, crypto, currentUser.UserId, AuditAction.NoteCreated,
            "Note", id.ToString(), "Success", currentUser.IpAddress, $"Title: {cmd.Title}", ct);
        await uow.SaveChangesAsync(ct);
        return id;
    }
}
