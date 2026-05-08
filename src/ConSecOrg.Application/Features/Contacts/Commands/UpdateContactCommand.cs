using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using MediatR;

namespace ConSecOrg.Application.Features.Contacts.Commands;

public record UpdateContactCommand(
    Guid ContactId,
    string Name,
    string? Email,
    string? Phone,
    string? Notes) : IRequest;

public class UpdateContactCommandHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<UpdateContactCommand>
{
    public async Task Handle(UpdateContactCommand cmd, CancellationToken ct)
    {
        var contact = await uow.Contacts.GetByIdAsync(cmd.ContactId, ct)
            ?? throw new NotFoundException("Contact", cmd.ContactId);

        if (contact.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        var key = currentUser.GetEncryptionKey();
        var enc = (string s) => crypto.Encrypt(System.Text.Encoding.UTF8.GetBytes(s), key);

        contact.Update(enc(cmd.Name),
            cmd.Email is not null ? enc(cmd.Email) : null,
            cmd.Phone is not null ? enc(cmd.Phone) : null,
            cmd.Notes is not null ? enc(cmd.Notes) : null);

        uow.Contacts.Update(contact);
        await uow.SaveChangesAsync(ct);
    }
}
