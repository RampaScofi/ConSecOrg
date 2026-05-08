using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using MediatR;

namespace ConSecOrg.Application.Features.Contacts.Commands;

public record DeleteContactCommand(Guid ContactId) : IRequest;

public class DeleteContactCommandHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<DeleteContactCommand>
{
    public async Task Handle(DeleteContactCommand cmd, CancellationToken ct)
    {
        var contact = await uow.Contacts.GetByIdAsync(cmd.ContactId, ct)
            ?? throw new NotFoundException("Contact", cmd.ContactId);

        if (contact.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        uow.Contacts.Delete(contact);
        await uow.SaveChangesAsync(ct);
    }
}
