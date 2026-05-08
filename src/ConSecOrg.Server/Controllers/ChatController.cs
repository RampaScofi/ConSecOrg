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
[Route("api/v1/chats")]
[Authorize]
public class ChatController(AppDbContext db, IHubContext<BoardHub> hub) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User ID claim missing."));

    /// <summary>
    /// Get list of all chats for current user — project group chats + direct chats with users
    /// who have exchanged messages with current user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatSummaryDto>>> GetChats(CancellationToken ct)
    {
        var myId = CurrentUserId;

        // 1. Project chats — every shared project the user is in
        var projectChats = await db.SharedProjects
            .AsNoTracking()
            .Where(p => p.OwnerUserId == myId || p.Members.Any(m => m.UserId == myId))
            .Select(p => new
            {
                p.Id,
                p.Name,
                LastMsg = db.ChatMessages
                    .Where(m => m.ProjectId == p.Id)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new { m.Text, m.SentAt, SenderName = m.Sender!.Username })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var result = projectChats.Select(p => new ChatSummaryDto
        {
            ChatId = $"project:{p.Id:N}",
            ChatType = "project",
            ProjectId = p.Id,
            Title = p.Name,
            LastMessageText = p.LastMsg?.Text ?? string.Empty,
            LastMessageAt = p.LastMsg?.SentAt,
            LastSenderUsername = p.LastMsg?.SenderName ?? string.Empty
        }).ToList();

        // 2. Direct chats — users who exchanged any messages with current user
        var partnerIds = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == null && (m.SenderUserId == myId || m.ToUserId == myId))
            .Select(m => m.SenderUserId == myId ? m.ToUserId!.Value : m.SenderUserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var partnerId in partnerIds)
        {
            var partner = await db.Users.AsNoTracking()
                .Where(u => u.Id == partnerId)
                .Select(u => new { u.Id, u.Username }).FirstOrDefaultAsync(ct);
            if (partner is null) continue;

            var lastMsg = await db.ChatMessages.AsNoTracking()
                .Where(m => m.ProjectId == null &&
                    ((m.SenderUserId == myId && m.ToUserId == partnerId) ||
                     (m.SenderUserId == partnerId && m.ToUserId == myId)))
                .OrderByDescending(m => m.SentAt)
                .Select(m => new { m.Text, m.SentAt, SenderName = m.Sender!.Username })
                .FirstOrDefaultAsync(ct);

            result.Add(new ChatSummaryDto
            {
                ChatId = $"user:{partnerId:N}",
                ChatType = "direct",
                OtherUserId = partnerId,
                Title = partner.Username,
                LastMessageText = lastMsg?.Text ?? string.Empty,
                LastMessageAt = lastMsg?.SentAt,
                LastSenderUsername = lastMsg?.SenderName ?? string.Empty
            });
        }

        return Ok(result.OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue).ToList());
    }

    [HttpGet("project/{projectId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetProjectMessages(Guid projectId, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var hasAccess = await db.SharedProjects.AsNoTracking().AnyAsync(p =>
            p.Id == projectId && (p.OwnerUserId == myId || p.Members.Any(m => m.UserId == myId)), ct);
        if (!hasAccess) return Forbid();

        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.Sender)
            .OrderBy(m => m.SentAt)
            .Select(m => MapDto(m))
            .ToListAsync(ct);

        return Ok(messages);
    }

    [HttpGet("direct/{otherUserId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetDirectMessages(Guid otherUserId, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == null &&
                ((m.SenderUserId == myId && m.ToUserId == otherUserId) ||
                 (m.SenderUserId == otherUserId && m.ToUserId == myId)))
            .Include(m => m.Sender)
            .OrderBy(m => m.SentAt)
            .Select(m => MapDto(m))
            .ToListAsync(ct);

        return Ok(messages);
    }

    [HttpPost]
    public async Task<ActionResult<ChatMessageDto>> SendMessage([FromBody] SendChatMessageDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text)) return BadRequest(new { message = "Сообщение не может быть пустым." });
        if (dto.ProjectId is null && dto.ToUserId is null)
            return BadRequest(new { message = "Не указан проект или получатель." });

        var myId = CurrentUserId;

        // Validate access
        if (dto.ProjectId.HasValue)
        {
            var hasAccess = await db.SharedProjects.AsNoTracking().AnyAsync(p =>
                p.Id == dto.ProjectId.Value && (p.OwnerUserId == myId || p.Members.Any(m => m.UserId == myId)), ct);
            if (!hasAccess) return Forbid();
        }

        var msg = new ChatMessage(Guid.NewGuid(), myId, dto.Text.Trim(), dto.ProjectId, dto.ToUserId);
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync(ct);

        var senderName = await db.Users.AsNoTracking()
            .Where(u => u.Id == myId).Select(u => u.Username).FirstAsync(ct);

        var result = new ChatMessageDto
        {
            Id = msg.Id,
            ProjectId = msg.ProjectId,
            SenderUserId = myId,
            SenderUsername = senderName,
            ToUserId = msg.ToUserId,
            Text = msg.Text,
            SentAt = msg.SentAt
        };

        // Broadcast
        if (dto.ProjectId.HasValue)
        {
            await hub.Clients.Group(BoardHub.ProjectGroup(dto.ProjectId.Value))
                .SendAsync("ChatMessageReceived", result, ct);
        }
        else if (dto.ToUserId.HasValue)
        {
            await hub.Clients.Group(BoardHub.UserGroup(dto.ToUserId.Value))
                .SendAsync("ChatMessageReceived", result, ct);
            // Echo back to sender for multi-device sync
            await hub.Clients.Group(BoardHub.UserGroup(myId))
                .SendAsync("ChatMessageReceived", result, ct);
        }

        return Ok(result);
    }

    private static ChatMessageDto MapDto(ChatMessage m) => new()
    {
        Id = m.Id,
        ProjectId = m.ProjectId,
        SenderUserId = m.SenderUserId,
        SenderUsername = m.Sender?.Username ?? string.Empty,
        ToUserId = m.ToUserId,
        Text = m.Text,
        SentAt = m.SentAt
    };
}
