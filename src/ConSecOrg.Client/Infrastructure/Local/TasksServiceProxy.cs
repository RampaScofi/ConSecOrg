using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Tasks;

namespace ConSecOrg.Client.Infrastructure.Local;

public sealed class TasksServiceProxy : ITasksApiService
{
    private readonly ModeService _mode;
    private readonly ApiClient _api;
    private readonly LocalTasksService _local;

    public TasksServiceProxy(ModeService mode, ApiClient api, LocalTasksService local)
    {
        _mode = mode;
        _api = api;
        _local = local;
    }

    private ITasksApiService Active => _mode.IsPersonal ? _local : _api;

    public Task<IReadOnlyList<TaskItemDto>> GetBoardAsync() => Active.GetBoardAsync();

    public Task<IReadOnlyList<TaskItemDto>> GetDueAsync(int days = 7) => Active.GetDueAsync(days);

    public Task<Guid> CreateTaskAsync(CreateTaskRequestDto request) => Active.CreateTaskAsync(request);

    public Task UpdateTaskAsync(Guid id, UpdateTaskRequestDto request) => Active.UpdateTaskAsync(id, request);

    public Task MoveTaskAsync(Guid id, MoveTaskRequestDto request) => Active.MoveTaskAsync(id, request);

    public Task DeleteTaskAsync(Guid id) => Active.DeleteTaskAsync(id);
}
