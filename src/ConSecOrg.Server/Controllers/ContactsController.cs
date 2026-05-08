using ConSecOrg.Application.Features.Contacts.Commands;
using ConSecOrg.Application.Features.Contacts.Queries;
using ConSecOrg.Shared.DTOs.Contacts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/contacts")]
[Authorize]
public class ContactsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetContactsQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContactDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetContactByIdQuery(id), ct));

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateContactRequestDto dto, CancellationToken ct)
    {
        var id = await sender.Send(new CreateContactCommand(dto.Name, dto.Email, dto.Phone, dto.Notes, dto.LinkedUserId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactRequestDto dto, CancellationToken ct)
    {
        await sender.Send(new UpdateContactCommand(id, dto.Name, dto.Email, dto.Phone, dto.Notes), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteContactCommand(id), ct);
        return NoContent();
    }
}
