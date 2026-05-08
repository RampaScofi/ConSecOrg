using ConSecOrg.Application.Features.Notes.Commands;
using ConSecOrg.Application.Features.Notes.Queries;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using ConSecOrg.Shared.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/notes")]
[Authorize]
public class NotesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<NoteDto>>> GetNotes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] SecurityLevelDto? level = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetNotesQuery(page, pageSize, categoryId, level, search), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteDto>> GetNote(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetNoteByIdQuery(id), ct);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        var result = await sender.Send(new SearchNotesQuery(q), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateNoteRequestDto dto, CancellationToken ct)
    {
        var id = await sender.Send(new CreateNoteCommand(
            dto.Title, dto.Content, dto.SecurityLevel,
            dto.CategoryId, dto.Tags, dto.IsTemplate, dto.IsPinned, dto.ExpiresAt), ct);
        return CreatedAtAction(nameof(GetNote), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequestDto dto, CancellationToken ct)
    {
        await sender.Send(new UpdateNoteCommand(
            id, dto.Title, dto.Content, dto.SecurityLevel,
            dto.CategoryId, dto.Tags, dto.IsPinned, dto.IsTemplate), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteNoteCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/timer")]
    public async Task<IActionResult> SetTimer(Guid id, [FromBody] SetTimerRequestDto dto, CancellationToken ct)
    {
        await sender.Send(new SetDestructionTimerCommand(id, dto.ExpiresAt), ct);
        return NoContent();
    }

    [HttpGet("expiring")]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> GetExpiring(
        [FromQuery] int hours = 1, CancellationToken ct = default)
    {
        var before = DateTime.UtcNow.AddHours(hours);
        var result = await sender.Send(new GetNotesQuery(1, 100), ct);
        var expiring = result.Items.Where(n => n.ExpiresAt.HasValue && n.ExpiresAt <= before).ToList();
        return Ok(expiring);
    }
}
