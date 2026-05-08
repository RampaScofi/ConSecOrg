using ConSecOrg.Application.Features.Tasks.Commands;
using ConSecOrg.Application.Features.Tasks.Queries;
using ConSecOrg.Shared.DTOs.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public class TasksController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItemDto>>> GetBoard(CancellationToken ct)
        => Ok(await sender.Send(new GetTasksByBoardQuery(), ct));

    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyList<TaskItemDto>>> GetDue([FromQuery] int days = 7, CancellationToken ct = default)
        => Ok(await sender.Send(new GetTasksDueQuery(days), ct));

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateTaskRequestDto dto, CancellationToken ct)
    {
        var id = await sender.Send(new CreateTaskCommand(dto.Title, dto.Description, dto.Priority, dto.DueDate), ct);
        return CreatedAtAction(nameof(GetBoard), new { }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequestDto dto, CancellationToken ct)
    {
        await sender.Send(new UpdateTaskCommand(id, dto.Title, dto.Description, dto.Status, dto.Priority, dto.DueDate), ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveTaskRequestDto dto, CancellationToken ct)
    {
        await sender.Send(new MoveTaskCommand(id, dto.NewStatus, dto.NewColumn, dto.NewPosition), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteTaskCommand(id), ct);
        return NoContent();
    }
}
