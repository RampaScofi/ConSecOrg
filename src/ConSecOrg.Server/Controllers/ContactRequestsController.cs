using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Shared.DTOs.Contacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/contacts/requests")]
[Authorize]
public class ContactRequestsController(AppDbContext db, ICryptoService crypto) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException());

    private async Task WriteAuditAsync(Guid userId, AuditAction action, string entityId, string details, CancellationToken ct)
    {
        var prev = await db.AuditLogs.OrderByDescending(l => l.Timestamp).FirstOrDefaultAsync(ct);
        var prevHash = prev?.CurrentHash ?? new byte[32];
        var ts = DateTime.UtcNow;
        var tsMs = new DateTime(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, ts.Second, ts.Millisecond, DateTimeKind.Utc);
        byte[] input =
        [
            .. prevHash,
            .. System.Text.Encoding.UTF8.GetBytes(tsMs.ToString("O")),
            .. System.Text.Encoding.UTF8.GetBytes(action.ToString()),
            .. System.Text.Encoding.UTF8.GetBytes(entityId),
            .. System.Text.Encoding.UTF8.GetBytes(userId.ToString()),
        ];
        var currentHash = crypto.Hash256(input);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        db.AuditLogs.Add(new AuditLog(Guid.NewGuid(), userId, action, "ContactRequest", entityId,
            "Success", ip, new HashChainEntry(prevHash, currentHash), ts, details));
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Send([FromBody] SendContactRequestDto dto, CancellationToken ct)
    {
        var myId = CurrentUserId;
        if (myId == dto.ReceiverId) return BadRequest("Cannot send request to yourself.");
        var exists = await db.ContactRequests.AnyAsync(r =>
            r.SenderId == myId && r.ReceiverId == dto.ReceiverId, ct);
        if (exists) return Conflict("Request already sent.");
        var request = new ContactRequest(Guid.NewGuid(), myId, dto.ReceiverId);
        db.ContactRequests.Add(request);
        await WriteAuditAsync(myId, AuditAction.ContactRequestSent, request.Id.ToString(),
            $"To: {dto.ReceiverId}", ct);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetPending), new { }, request.Id);
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<ContactRequestDto>>> GetPending(CancellationToken ct)
    {
        var myId = CurrentUserId;
        var requests = await db.ContactRequests
            .Include(r => r.Sender)
            .Include(r => r.Receiver)
            .Where(r => r.ReceiverId == myId && r.Status == ContactRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ContactRequestDto
            {
                Id = r.Id,
                SenderId = r.SenderId,
                SenderUsername = r.Sender!.Username,
                SenderEmail = r.Sender!.Email,
                ReceiverId = r.ReceiverId,
                ReceiverUsername = r.Receiver!.Username,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(requests);
    }

    [HttpGet("sent")]
    public async Task<ActionResult<IReadOnlyList<ContactRequestDto>>> GetSent(CancellationToken ct)
    {
        var myId = CurrentUserId;
        var requests = await db.ContactRequests
            .Include(r => r.Sender)
            .Include(r => r.Receiver)
            .Where(r => r.SenderId == myId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ContactRequestDto
            {
                Id = r.Id,
                SenderId = r.SenderId,
                SenderUsername = r.Sender!.Username,
                SenderEmail = r.Sender!.Email,
                ReceiverId = r.ReceiverId,
                ReceiverUsername = r.Receiver!.Username,
                ReceiverEmail = r.Receiver!.Email,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(requests);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var request = await db.ContactRequests
            .Include(r => r.Sender)
            .FirstOrDefaultAsync(r => r.Id == id && r.ReceiverId == myId, ct);
        if (request is null) return NotFound();
        request.Accept();
        await WriteAuditAsync(myId, AuditAction.ContactRequestAccepted, id.ToString(),
            $"From: {request.SenderId}", ct);
        await db.SaveChangesAsync(ct);
        return Ok(new { request.SenderId, SenderUsername = request.Sender!.Username, SenderEmail = request.Sender.Email });
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var request = await db.ContactRequests.FirstOrDefaultAsync(r => r.Id == id && r.ReceiverId == myId, ct);
        if (request is null) return NotFound();
        request.Decline();
        await WriteAuditAsync(myId, AuditAction.ContactRequestDeclined, id.ToString(), string.Empty, ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
