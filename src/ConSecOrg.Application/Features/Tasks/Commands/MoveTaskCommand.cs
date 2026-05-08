using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Helpers;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Tasks.Commands;

public record MoveTaskCommand(
    Guid TaskId,
    TaskStatusDto NewStatus,
    int NewColumn,
    int NewPosition) : IRequest;

public class MoveTaskCommandHandler(IUnitOfWork uow, ICryptoService crypto, ICurrentUserContext currentUser)
    : IRequestHandler<MoveTaskCommand>
{
    public async Task Handle(MoveTaskCommand cmd, CancellationToken ct)
    {
        var task = await uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new NotFoundException("TaskItem", cmd.TaskId);

        if (task.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        task.MoveTo((TaskItemStatus)(int)cmd.NewStatus, cmd.NewColumn, cmd.NewPosition);
        uow.Tasks.Update(task);
        await AuditHelper.WriteAsync(uow, crypto, currentUser.UserId, AuditAction.TaskMoved,
            "TaskItem", cmd.TaskId.ToString(), "Success", currentUser.IpAddress,
            $"Column: {cmd.NewColumn}, Status: {cmd.NewStatus}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
