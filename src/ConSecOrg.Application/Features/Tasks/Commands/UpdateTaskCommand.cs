using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Helpers;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Tasks.Commands;

public record UpdateTaskCommand(
    Guid TaskId,
    string Title,
    string? Description,
    TaskStatusDto Status,
    TaskPriorityDto Priority,
    DateTime? DueDate) : IRequest;

public class UpdateTaskCommandHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<UpdateTaskCommand>
{
    public async Task Handle(UpdateTaskCommand cmd, CancellationToken ct)
    {
        var task = await uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new NotFoundException("TaskItem", cmd.TaskId);

        if (task.UserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
            throw new ForbiddenException();

        var priority = (TaskPriority)(int)cmd.Priority;
        var status = (TaskItemStatus)(int)cmd.Status;

        Domain.ValueObjects.EncryptedContent? encDesc = null;
        if (cmd.Description is not null)
        {
            var encKey = currentUser.GetEncryptionKey();
            encDesc = crypto.Encrypt(System.Text.Encoding.UTF8.GetBytes(cmd.Description), encKey);
        }

        task.UpdateDetails(cmd.Title, encDesc, priority, cmd.DueDate);
        task.MoveTo(status, task.BoardColumn, task.ColumnPosition);
        uow.Tasks.Update(task);
        await AuditHelper.WriteAsync(uow, crypto, currentUser.UserId, AuditAction.TaskUpdated,
            "TaskItem", cmd.TaskId.ToString(), "Success", currentUser.IpAddress, $"Title: {cmd.Title}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
