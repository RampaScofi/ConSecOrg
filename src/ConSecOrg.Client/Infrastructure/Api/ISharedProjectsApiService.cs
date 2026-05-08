using ConSecOrg.Shared.DTOs.Projects;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface ISharedProjectsApiService
{
    Task<IReadOnlyList<SharedProjectDto>> GetMyProjectsAsync();
    Task<SharedProjectDto> CreateProjectAsync(CreateSharedProjectDto request);
    Task<SharedProjectDto> JoinProjectAsync(JoinSharedProjectDto request);
    Task DeleteProjectAsync(Guid id);
    Task LeaveProjectAsync(Guid id);

    // Columns
    Task<IReadOnlyList<SharedProjectColumnDto>> GetColumnsAsync(Guid projectId);
    Task<SharedProjectColumnDto> CreateColumnAsync(Guid projectId, CreateSharedProjectColumnDto request);
    Task<SharedProjectColumnDto> UpdateColumnAsync(Guid projectId, Guid columnId, UpdateSharedProjectColumnDto request);
    Task DeleteColumnAsync(Guid projectId, Guid columnId);
    Task ReorderColumnsAsync(Guid projectId, List<Guid> orderedIds);

    // Tasks
    Task<IReadOnlyList<SharedProjectTaskDto>> GetProjectTasksAsync(Guid projectId);
    Task<SharedProjectTaskDto> CreateProjectTaskAsync(Guid projectId, CreateSharedProjectTaskDto request);
    Task<SharedProjectTaskDto> UpdateProjectTaskAsync(Guid projectId, Guid taskId, UpdateSharedProjectTaskDto request);
    Task MoveProjectTaskAsync(Guid projectId, Guid taskId, MoveSharedTaskDto request);
    Task DeleteProjectTaskAsync(Guid projectId, Guid taskId);
}
