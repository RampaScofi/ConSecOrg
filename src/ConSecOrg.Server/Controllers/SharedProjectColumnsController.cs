using ConSecOrg.Domain.Entities;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Server.Hubs;
using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/columns")]
[Authorize]
public class SharedProjectColumnsController(AppDbContext db, IHubContext<BoardHub> hub) : ControllerBase
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
    public async Task<ActionResult<IReadOnlyList<SharedProjectColumnDto>>> GetAll(
        Guid projectId, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var columns = await db.SharedProjectColumns
            .AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Order)
            .ToListAsync(ct);
        return Ok(columns.Select(MapDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SharedProjectColumnDto>> Create(
        Guid projectId, [FromBody] CreateSharedProjectColumnDto dto, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Название обязательно." });

        var maxOrder = await db.SharedProjectColumns
            .Where(c => c.ProjectId == projectId)
            .Select(c => (int?)c.Order).MaxAsync(ct) ?? -1;

        var entity = new SharedProjectColumn(projectId, dto.Name.Trim(), dto.Color, maxOrder + 1);
        db.SharedProjectColumns.Add(entity);
        await db.SaveChangesAsync(ct);

        var result = MapDto(entity);
        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedColumnCreated", result, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SharedProjectColumnDto>> Update(
        Guid projectId, Guid id, [FromBody] UpdateSharedProjectColumnDto dto, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var col = await db.SharedProjectColumns
            .FirstOrDefaultAsync(c => c.Id == id && c.ProjectId == projectId, ct);
        if (col is null) return NotFound();

        col.Update(dto.Name, dto.Color, dto.Order);
        await db.SaveChangesAsync(ct);

        var result = MapDto(col);
        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedColumnUpdated", result, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid id, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var deleted = await db.SharedProjectColumns
            .Where(c => c.Id == id && c.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0) return NotFound();

        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedColumnDeleted", id, ct);
        return NoContent();
    }

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(
        Guid projectId, [FromBody] List<Guid> orderedIds, CancellationToken ct)
    {
        if (!await HasAccessAsync(projectId, ct)) return Forbid();
        var columns = await db.SharedProjectColumns
            .Where(c => c.ProjectId == projectId)
            .ToListAsync(ct);

        for (int i = 0; i < orderedIds.Count; i++)
        {
            var col = columns.FirstOrDefault(c => c.Id == orderedIds[i]);
            if (col != null) col.SetOrder(i);
        }
        await db.SaveChangesAsync(ct);

        await hub.Clients.Group(BoardHub.ProjectGroup(projectId))
            .SendAsync("SharedColumnsReordered", orderedIds, ct);
        return Ok();
    }

    private static SharedProjectColumnDto MapDto(SharedProjectColumn c) => new()
    {
        Id = c.Id,
        ProjectId = c.ProjectId,
        Name = c.Name,
        Color = c.Color,
        Order = c.Order
    };
}
