using ConSecOrg.Domain.Entities;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Server.Hubs;
using ConSecOrg.Server.Services;
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
public class ChatController(AppDbContext db, IHubContext<BoardHub> hub, ChatEncryptionService chatCrypto) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User ID claim missing."));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChatSummaryDto>>> GetChats(CancellationToken ct)
    {
        var myId = CurrentUserId;
        var result = new List<ChatSummaryDto>();

        // Load last-read timestamps for current user (keyed by chatKey)
        var lastReads = await db.ChatLastReads
            .AsNoTracking()
            .Where(r => r.UserId == myId)
            .ToDictionaryAsync(r => r.ChatKey, r => r.LastReadAt, ct);

        // 1. Project chats
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

        foreach (var p in projectChats)
        {
            var chatKey = $"project:{p.Id:N}";
            var lastRead = lastReads.TryGetValue(chatKey, out var lr) ? lr : (DateTime?)null;
            var unread = lastRead.HasValue
                ? await db.ChatMessages.CountAsync(
                    m => m.ProjectId == p.Id && m.SenderUserId != myId && m.SentAt > lastRead.Value, ct)
                : await db.ChatMessages.CountAsync(
                    m => m.ProjectId == p.Id && m.SenderUserId != myId, ct);
            result.Add(new ChatSummaryDto
            {
                ChatId = chatKey,
                ChatType = "project",
                ProjectId = p.Id,
                Title = p.Name,
                LastMessageText = p.LastMsg?.Text ?? string.Empty,
                LastMessageAt = p.LastMsg?.SentAt,
                LastSenderUsername = p.LastMsg?.SenderName ?? string.Empty,
                UnreadCount = unread
            });
        }

        // 2. Direct chats
        var partnerIds = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == null && m.GroupChatId == null &&
                       (m.SenderUserId == myId || m.ToUserId == myId))
            .Select(m => m.SenderUserId == myId ? m.ToUserId!.Value : m.SenderUserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var partnerId in partnerIds)
        {
            var partner = await db.Users.AsNoTracking()
                .Where(u => u.Id == partnerId)
                .Select(u => new { u.Id, u.Username }).FirstOrDefaultAsync(ct);
            if (partner is null) continue;

            var chatKey = $"user:{partnerId:N}";
            var lastRead = lastReads.TryGetValue(chatKey, out var lr) ? lr : (DateTime?)null;
            var unread = lastRead.HasValue
                ? await db.ChatMessages.CountAsync(m =>
                    m.ProjectId == null && m.GroupChatId == null &&
                    m.SenderUserId == partnerId && m.ToUserId == myId &&
                    m.SentAt > lastRead.Value, ct)
                : await db.ChatMessages.CountAsync(m =>
                    m.ProjectId == null && m.GroupChatId == null &&
                    m.SenderUserId == partnerId && m.ToUserId == myId, ct);

            var lastMsg = await db.ChatMessages.AsNoTracking()
                .Where(m => m.ProjectId == null && m.GroupChatId == null &&
                    ((m.SenderUserId == myId && m.ToUserId == partnerId) ||
                     (m.SenderUserId == partnerId && m.ToUserId == myId)))
                .OrderByDescending(m => m.SentAt)
                .Select(m => new { m.Text, m.SentAt, SenderName = m.Sender!.Username })
                .FirstOrDefaultAsync(ct);

            result.Add(new ChatSummaryDto
            {
                ChatId = chatKey,
                ChatType = "direct",
                OtherUserId = partnerId,
                Title = partner.Username,
                LastMessageText = lastMsg?.Text ?? string.Empty,
                LastMessageAt = lastMsg?.SentAt,
                LastSenderUsername = lastMsg?.SenderName ?? string.Empty,
                UnreadCount = unread
            });
        }

        // 3. Group chats
        var groups = await db.GroupChats
            .AsNoTracking()
            .Where(g => g.CreatedByUserId == myId || g.Members.Any(m => m.UserId == myId))
            .Select(g => new
            {
                g.Id,
                g.Name,
                LastMsg = db.ChatMessages
                    .Where(m => m.GroupChatId == g.Id)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => new { m.Text, m.SentAt, SenderName = m.Sender!.Username })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        foreach (var g in groups)
        {
            var chatKey = $"group:{g.Id:N}";
            var lastRead = lastReads.TryGetValue(chatKey, out var lr) ? lr : (DateTime?)null;
            var unread = lastRead.HasValue
                ? await db.ChatMessages.CountAsync(
                    m => m.GroupChatId == g.Id && m.SenderUserId != myId && m.SentAt > lastRead.Value, ct)
                : await db.ChatMessages.CountAsync(
                    m => m.GroupChatId == g.Id && m.SenderUserId != myId, ct);
            result.Add(new ChatSummaryDto
            {
                ChatId = chatKey,
                ChatType = "group",
                GroupChatId = g.Id,
                Title = g.Name,
                LastMessageText = g.LastMsg?.Text ?? string.Empty,
                LastMessageAt = g.LastMsg?.SentAt,
                LastSenderUsername = g.LastMsg?.SenderName ?? string.Empty,
                UnreadCount = unread
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
            .ToListAsync(ct);

        return Ok(messages.Select(MapDto).ToList());
    }

    [HttpGet("direct/{otherUserId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetDirectMessages(Guid otherUserId, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.ProjectId == null && m.GroupChatId == null &&
                ((m.SenderUserId == myId && m.ToUserId == otherUserId) ||
                 (m.SenderUserId == otherUserId && m.ToUserId == myId)))
            .Include(m => m.Sender)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

        // Check when the OTHER user last read our conversation
        // otherUser's chatKey for this DM = "user:{myId:N}"
        var otherLastRead = await db.ChatLastReads.AsNoTracking()
            .Where(r => r.UserId == otherUserId && r.ChatKey == $"user:{myId:N}")
            .Select(r => (DateTime?)r.LastReadAt)
            .FirstOrDefaultAsync(ct);

        var dtos = messages.Select(m =>
        {
            var dto = MapDto(m);
            // Message is read if sender=me and partner has read at/after this message's time
            if (m.SenderUserId == myId && otherLastRead.HasValue)
                dto.IsReadByRecipient = m.SentAt <= otherLastRead.Value;
            return dto;
        }).ToList();

        return Ok(dtos);
    }

    // Mark a chat as read: upsert last-read timestamp and notify partner via SignalR
    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ChatKey)) return BadRequest();
        var myId = CurrentUserId;
        var now = DateTime.UtcNow;

        var existing = await db.ChatLastReads
            .FirstOrDefaultAsync(r => r.UserId == myId && r.ChatKey == dto.ChatKey, ct);
        if (existing is null)
            db.ChatLastReads.Add(new ChatLastRead { Id = Guid.NewGuid(), UserId = myId, ChatKey = dto.ChatKey, LastReadAt = now });
        else
            existing.LastReadAt = now;
        await db.SaveChangesAsync(ct);

        // For direct chat: notify the partner that we read their messages
        // ChatKey format: "user:{partnerId:N}" → notify that partner
        if (dto.ChatKey.StartsWith("user:") && Guid.TryParse(dto.ChatKey[5..], out var partnerId))
        {
            await hub.Clients.Group(BoardHub.UserGroup(partnerId))
                .SendAsync("MessagesRead", $"user:{myId:N}", ct);
        }

        return Ok();
    }

    [HttpPost]
    public async Task<ActionResult<ChatMessageDto>> SendMessage([FromBody] SendChatMessageDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text) && string.IsNullOrWhiteSpace(dto.AttachmentFileId))
            return BadRequest(new { message = "Сообщение не может быть пустым." });
        if (dto.ProjectId is null && dto.ToUserId is null && dto.GroupChatId is null)
            return BadRequest(new { message = "Не указан получатель." });

        var myId = CurrentUserId;

        if (dto.ProjectId.HasValue)
        {
            var hasAccess = await db.SharedProjects.AsNoTracking().AnyAsync(p =>
                p.Id == dto.ProjectId.Value && (p.OwnerUserId == myId || p.Members.Any(m => m.UserId == myId)), ct);
            if (!hasAccess) return Forbid();
        }

        if (dto.GroupChatId.HasValue)
        {
            var isMember = await db.GroupChatMembers.AnyAsync(m => m.GroupChatId == dto.GroupChatId.Value && m.UserId == myId, ct)
                        || await db.GroupChats.AnyAsync(g => g.Id == dto.GroupChatId.Value && g.CreatedByUserId == myId, ct);
            if (!isMember) return Forbid();
        }

        var text = (dto.Text?.Trim() ?? string.Empty);
        if (string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(dto.AttachmentFileName))
            text = $"📎 {dto.AttachmentFileName}";

        var msg = new ChatMessage(
            Guid.NewGuid(), myId, text,
            dto.ProjectId, dto.ToUserId, dto.GroupChatId,
            dto.AttachmentFileId, dto.AttachmentFileName, dto.AttachmentSize);

        // Encrypt message text at rest using ГОСТ Р 34.12-2015
        if (!string.IsNullOrEmpty(text))
        {
            var (cipher, nonce, hmac) = chatCrypto.Encrypt(text);
            msg.SetEncryptedText(cipher, nonce, hmac);
        }

        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync(ct);

        var senderName = await db.Users.AsNoTracking()
            .Where(u => u.Id == myId).Select(u => u.Username).FirstAsync(ct);

        var result = MapDto(msg);
        result.SenderUsername = senderName;

        if (dto.ProjectId.HasValue)
        {
            await hub.Clients.Group(BoardHub.ProjectGroup(dto.ProjectId.Value))
                .SendAsync("ChatMessageReceived", result, ct);
        }
        else if (dto.GroupChatId.HasValue)
        {
            await hub.Clients.Group(BoardHub.GroupChatGroup(dto.GroupChatId.Value))
                .SendAsync("ChatMessageReceived", result, ct);
        }
        else if (dto.ToUserId.HasValue)
        {
            await hub.Clients.Group(BoardHub.UserGroup(dto.ToUserId.Value))
                .SendAsync("ChatMessageReceived", result, ct);
            await hub.Clients.Group(BoardHub.UserGroup(myId))
                .SendAsync("ChatMessageReceived", result, ct);
        }

        return Ok(result);
    }

    private ChatMessageDto MapDto(ChatMessage m) => new()
    {
        Id = m.Id,
        ProjectId = m.ProjectId,
        GroupChatId = m.GroupChatId,
        SenderUserId = m.SenderUserId,
        SenderUsername = m.Sender?.Username ?? string.Empty,
        ToUserId = m.ToUserId,
        Text = m.IsTextEncrypted
            ? chatCrypto.Decrypt(m.TextCipher!, m.TextNonce!, m.TextHmac!)
            : m.Text,
        SentAt = m.SentAt,
        AttachmentFileId = m.AttachmentFileId,
        AttachmentFileName = m.AttachmentFileName,
        AttachmentSize = m.AttachmentSize
    };
}

public class MarkReadRequestDto
{
    public string ChatKey { get; set; } = string.Empty;
}
