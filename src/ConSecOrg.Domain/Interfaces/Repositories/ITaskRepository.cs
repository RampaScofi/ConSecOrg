using ConSecOrg.Domain.Entities;

namespace ConSecOrg.Domain.Interfaces.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetDueAsync(Guid userId, int daysAhead, CancellationToken ct = default);
    Task AddAsync(TaskItem task, CancellationToken ct = default);
    void Update(TaskItem task);
    void Delete(TaskItem task);
}
