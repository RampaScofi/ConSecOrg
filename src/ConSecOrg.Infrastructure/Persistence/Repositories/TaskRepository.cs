using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Infrastructure.Persistence.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _db;

    public TaskRepository(AppDbContext db) => _db = db;

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TaskItem>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.TaskItems
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.BoardColumn).ThenBy(t => t.ColumnPosition)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetDueAsync(Guid userId, int daysAhead, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddDays(daysAhead);
        return await _db.TaskItems
            .Where(t => t.UserId == userId && t.DueDate != null && t.DueDate <= deadline)
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TaskItem task, CancellationToken ct = default)
        => await _db.TaskItems.AddAsync(task, ct);

    public void Update(TaskItem task) => _db.TaskItems.Update(task);

    public void Delete(TaskItem task) => _db.TaskItems.Remove(task);
}
