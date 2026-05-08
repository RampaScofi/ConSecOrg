using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Tasks.Queries;

public record GetTasksDueQuery(int DaysAhead = 7) : IRequest<IReadOnlyList<TaskItemDto>>;

public class GetTasksDueQueryHandler(
    IUnitOfWork uow,
    ICryptoService crypto,
    ICurrentUserContext currentUser) : IRequestHandler<GetTasksDueQuery, IReadOnlyList<TaskItemDto>>
{
    public async Task<IReadOnlyList<TaskItemDto>> Handle(GetTasksDueQuery query, CancellationToken ct)
    {
        var tasks = await uow.Tasks.GetDueAsync(currentUser.UserId, query.DaysAhead, ct);
        var encKey = currentUser.GetEncryptionKey();

        return tasks.Select(t =>
        {
            string? desc = null;
            if (t.Description is not null)
            {
                try { desc = System.Text.Encoding.UTF8.GetString(crypto.Decrypt(t.Description, encKey)); }
                catch { desc = null; }
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
        }).ToList();
    }
}
