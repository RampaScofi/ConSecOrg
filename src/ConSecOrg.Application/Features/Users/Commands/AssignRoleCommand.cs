using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using MediatR;

namespace ConSecOrg.Application.Features.Users.Commands;

public record AssignRoleCommand(Guid TargetUserId, Guid RoleId) : IRequest;

public class AssignRoleCommandHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<AssignRoleCommand>
{
    public async Task Handle(AssignRoleCommand cmd, CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Only Admin can assign roles.");

        var user = await uow.Users.GetByIdAsync(cmd.TargetUserId, ct)
            ?? throw new NotFoundException("User", cmd.TargetUserId);

        user.AssignRole(cmd.RoleId);
        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);
    }
}
