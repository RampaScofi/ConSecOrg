using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Helpers;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using MediatR;

namespace ConSecOrg.Application.Features.Tasks.Commands;

public record DeleteTaskCommand(Guid TaskId) : IRequest;

public class DeleteTaskCommandHandler(IUnitOfWork uow, ICryptoService crypto, ICurrentUserContext currentUser)
    : IRequestHandler<DeleteTaskCommand>
{
    public async Task Handle(DeleteTaskCommand cmd, CancellationToken ct)
    {
        var task = await uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new NotFoundException("TaskItem", cmd.TaskId);

        if (task.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        uow.Tasks.Delete(task);
        await AuditHelper.WriteAsync(uow, crypto, currentUser.UserId, AuditAction.TaskDeleted,
            "TaskItem", cmd.TaskId.ToString(), "Success", currentUser.IpAddress, $"Title: {task.Title}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
