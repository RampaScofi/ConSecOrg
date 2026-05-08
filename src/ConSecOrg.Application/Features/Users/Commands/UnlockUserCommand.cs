using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using MediatR;

namespace ConSecOrg.Application.Features.Users.Commands;

public record UnlockUserCommand(Guid TargetUserId) : IRequest;

public class UnlockUserCommandHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<UnlockUserCommand>
{
    public async Task Handle(UnlockUserCommand cmd, CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Only Admin can unlock users.");

        var user = await uow.Users.GetByIdAsync(cmd.TargetUserId, ct)
            ?? throw new NotFoundException("User", cmd.TargetUserId);

        user.Unlock();
        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);
    }
}
