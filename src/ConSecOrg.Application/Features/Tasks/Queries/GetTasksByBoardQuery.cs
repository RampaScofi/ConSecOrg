using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Tasks.Queries;

public record GetTasksByBoardQuery : IRequest<IReadOnlyList<TaskItemDto>>;

public class GetTasksByBoardQueryHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<GetTasksByBoardQuery, IReadOnlyList<TaskItemDto>>
{
    public async Task<IReadOnlyList<TaskItemDto>> Handle(GetTasksByBoardQuery query, CancellationToken ct)
    {
        var tasks = await uow.Tasks.GetByUserAsync(currentUser.UserId, ct);
        var encKey = currentUser.GetEncryptionKey();

        return tasks.Select(t => MapToDto(t, encKey)).ToList();
    }

    private TaskItemDto MapToDto(Domain.Entities.TaskItem t, byte[] key)
    {
        string? desc = null;
        if (t.Description is not null)
        {
            try { desc = System.Text.Encoding.UTF8.GetString(crypto.Decrypt(t.Description, key)); }
            catch { desc = "[Ошибка расшифровки]"; }
        }
        return new TaskItemDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = desc,
            Status = (TaskStatusDto)(int)t.Status,
            Priority = (TaskPriorityDto)(int)t.Priority,
            BoardColumn = t.BoardColumn,
            ColumnPosition = t.ColumnPosition,
            DueDate = t.DueDate,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }
}
