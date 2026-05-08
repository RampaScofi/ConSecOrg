using ConSecOrg.Domain.Entities;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Server.Hubs;
using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/tasks")]
[Authorize]
public class SharedProjectTasksController(AppDbContext db, IHubContext<BoardHub> hub) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException());

    private async Task<bool> HasAccessAsync(Guid projectId, CancellationToken ct) =>
        await db.SharedProjects.AsNoTracking().AnyAsync(p =>
            p.Id == projectId &&
            (p.OwnerUserId == CurrentUserId || p.Members.Any(m => m.UserId == CurrentUserId)), ct);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SharedProjectTaskDto>>> GetAll(Guid projectId, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var tasks = await db.SharedProjectTasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.AssignedUser)
            .OrderBy(t => t.Position)
            .ToListAsync(ct);
        return Ok(tasks.Select(MapDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SharedProjectTaskDto>> Create(Guid projectId,
        [FromBody] CreateSharedProjectTaskDto dto, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "Заголовок обязателен." });

        var maxPos = await db.SharedProjectTasks
            .Where(t => t.ProjectId == projectId && t.ColumnId == dto.ColumnId)
            .Select(t => (int?)t.Position).MaxAsync(ct) ?? -1;

        var entity = new SharedProjectTask(Guid.NewGuid(), projectId, dto.Title.Trim(), dto.ColumnId, maxPos + 1);
        entity.Update(entity.Title, dto.Description, dto.ColumnId, dto.Priority, dto.DueDate,
            false, entity.Position, dto.AssignedUserId, JsonSerializer.Serialize(dto.Tags));
        db.SharedProjectTasks.Add(entity);
        await db.SaveChangesAsync(ct);

        var created = await db.SharedProjectTasks.AsNoTracking()
            .Include(t => t.AssignedUser)
            .FirstAsync(t => t.Id == entity.Id, ct);

        var result = MapDto(created);
        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedTaskCreated", result, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SharedProjectTaskDto>> Update(Guid projectId, Guid id,
        [FromBody] UpdateSharedProjectTaskDto dto, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var task = await db.SharedProjectTasks
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId, ct);
        if (task is null) return NotFound();

        task.Update(dto.Title, dto.Description, dto.ColumnId, dto.Priority, dto.DueDate,
            dto.IsCompleted, dto.Position, dto.AssignedUserId, JsonSerializer.Serialize(dto.Tags));
        await db.SaveChangesAsync(ct);

        var updated = await db.SharedProjectTasks.AsNoTracking()
            .Include(t => t.AssignedUser)
            .FirstAsync(t => t.Id == id, ct);

        var result = MapDto(updated);
        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedTaskUpdated", result, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid projectId, Guid id,
        [FromBody] MoveSharedTaskDto dto, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var task = await db.SharedProjectTasks
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId, ct);
        if (task is null) return NotFound();

        task.MoveToColumn(dto.ColumnId, dto.Position);
        await db.SaveChangesAsync(ct);

        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedTaskMoved", new { id, dto.ColumnId, dto.Position }, ct);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var deleted = await db.SharedProjectTasks
            .Where(t => t.Id == id && t.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) return NotFound();

        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedTaskDeleted", id, ct);
        return NoContent();
    }

    private static SharedProjectTaskDto MapDto(SharedProjectTask t)
    {
        var tags = new List<string>();
        try { if (!string.IsNullOrEmpty(t.Tags)) tags = JsonSerializer.Deserialize<List<string>>(t.Tags) ?? []; }
        catch { }
        return new SharedProjectTaskDto
        {
            Id = t.Id,
            ProjectId = t.ProjectId,
            ColumnId = t.ColumnId,
            Title = t.Title,
            Description = t.Description,
            Tags = tags,
            Priority = t.Priority,
            DueDate = t.DueDate,
            Position = t.Position,
            IsCompleted = t.IsCompleted,
            AssignedUserId = t.AssignedUserId,
            AssignedUsername = t.AssignedUser?.Username,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }
}
