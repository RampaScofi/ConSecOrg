using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Crypto;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ConSecOrg.Client.Infrastructure.Local;

public sealed class LocalTasksService : ITasksApiService
{
    private readonly PersonalDbContext _db;
    private readonly GostCryptoService _crypto = new();
    private readonly SessionService _session;
    private readonly LocalUserStore _userStore;

    public LocalTasksService(PersonalDbContext db, SessionService session, LocalUserStore userStore)
    {
        _db = db;
        _session = session;
        _userStore = userStore;
    }

    private byte[]? Key => _session.GetEncryptionKey();

    public async Task<IReadOnlyList<TaskItemDto>> GetBoardAsync()
    {
        var tasks = await _db.TaskItems
            .Where(t => t.UserId == _userStore.UserId)
            .OrderBy(t => t.BoardColumn)
            .ThenBy(t => t.ColumnPosition)
            .ToListAsync();

        return tasks.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TaskItemDto>> GetDueAsync(int days = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        var tasks = await _db.TaskItems
            .Where(t => t.UserId == _userStore.UserId && t.DueDate <= cutoff && t.Status != TaskItemStatus.Done)
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        return tasks.Select(ToDto).ToList();
    }

    public async Task<Guid> CreateTaskAsync(CreateTaskRequestDto request)
    {
        var id = Guid.NewGuid();
        var task = new TaskItem(id, _userStore.UserId, request.Title,
            (TaskPriority)(int)request.Priority);

        EncryptedContent? encDesc = null;
        if (!string.IsNullOrWhiteSpace(request.Description) && Key is not null)
        {
            var pt = Encoding.UTF8.GetBytes(request.Description);
            encDesc = _crypto.Encrypt(pt, Key);
            Array.Clear(pt, 0, pt.Length);
        }

        task.UpdateDetails(request.Title, encDesc, (TaskPriority)(int)request.Priority, request.DueDate);

        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();
        return id;
    }

    public async Task UpdateTaskAsync(Guid id, UpdateTaskRequestDto request)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == _userStore.UserId)
            ?? throw new KeyNotFoundException($"Задача {id} не найдена.");

        EncryptedContent? encDesc = null;
        if (!string.IsNullOrWhiteSpace(request.Description) && Key is not null)
        {
            var pt = Encoding.UTF8.GetBytes(request.Description);
            encDesc = _crypto.Encrypt(pt, Key);
            Array.Clear(pt, 0, pt.Length);
        }

        task.UpdateDetails(request.Title, encDesc, (TaskPriority)(int)request.Priority, request.DueDate);

        var newStatus = (TaskItemStatus)(int)request.Status;
        int col = (int)newStatus;
        task.MoveTo(newStatus, col, task.ColumnPosition);

        await _db.SaveChangesAsync();
    }

    public async Task MoveTaskAsync(Guid id, MoveTaskRequestDto request)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == _userStore.UserId)
            ?? throw new KeyNotFoundException($"Задача {id} не найдена.");

        task.MoveTo((TaskItemStatus)(int)request.NewStatus, request.NewColumn, request.NewPosition);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTaskAsync(Guid id)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == _userStore.UserId);
        if (task is null) return;
        _db.TaskItems.Remove(task);
        await _db.SaveChangesAsync();
    }

    private TaskItemDto ToDto(TaskItem t)
    {
        string? description = null;
        if (t.Description is not null && Key is not null)
        {
            try
            {
                var pt = _crypto.Decrypt(t.Description, Key);
                description = Encoding.UTF8.GetString(pt);
                Array.Clear(pt, 0, pt.Length);
            }
            catch { /* integrity failure */ }
        }

        return new TaskItemDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = description,
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
