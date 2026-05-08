using ConSecOrg.Application.Common.Helpers;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.Enums;
using FluentValidation;
using MediatR;

namespace ConSecOrg.Application.Features.Tasks.Commands;

public record CreateTaskCommand(
    string Title,
    string? Description,
    TaskPriorityDto Priority,
    DateTime? DueDate) : IRequest<Guid>;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
    }
}

public class CreateTaskCommandHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<CreateTaskCommand, Guid>
{
    public async Task<Guid> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        var priority = (TaskPriority)(int)cmd.Priority;
        var id = Guid.NewGuid();
        var task = new TaskItem(id, currentUser.UserId, cmd.Title, priority);

        if (cmd.Description is not null)
        {
            var encKey = currentUser.GetEncryptionKey();
            var encrypted = crypto.Encrypt(System.Text.Encoding.UTF8.GetBytes(cmd.Description), encKey);
            task.UpdateDetails(cmd.Title, encrypted, priority, cmd.DueDate);
        }
        else
        {
            task.UpdateDetails(cmd.Title, null, priority, cmd.DueDate);
        }

        await uow.Tasks.AddAsync(task, ct);
        await AuditHelper.WriteAsync(uow, crypto, currentUser.UserId, AuditAction.TaskCreated,
            "TaskItem", id.ToString(), "Success", currentUser.IpAddress, $"Title: {cmd.Title}", ct);
        await uow.SaveChangesAsync(ct);
        return id;
    }
}
