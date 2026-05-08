using ConSecOrg.Shared.DTOs.Tasks;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface ITasksApiService
{
    Task<IReadOnlyList<TaskItemDto>> GetBoardAsync();
    Task<IReadOnlyList<TaskItemDto>> GetDueAsync(int days = 7);
    Task<Guid> CreateTaskAsync(CreateTaskRequestDto request);
    Task UpdateTaskAsync(Guid id, UpdateTaskRequestDto request);
    Task MoveTaskAsync(Guid id, MoveTaskRequestDto request);
    Task DeleteTaskAsync(Guid id);
}
