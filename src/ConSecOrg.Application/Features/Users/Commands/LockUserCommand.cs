using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using MediatR;

namespace ConSecOrg.Application.Features.Users.Commands;

public record LockUserCommand(Guid TargetUserId) : IRequest;

public class LockUserCommandHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<LockUserCommand>
{
    public async Task Handle(LockUserCommand cmd, CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Only Admin can lock users.");

        var user = await uow.Users.GetByIdAsync(cmd.TargetUserId, ct)
            ?? throw new NotFoundException("User", cmd.TargetUserId);

        user.Lock();
        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);
    }
}
