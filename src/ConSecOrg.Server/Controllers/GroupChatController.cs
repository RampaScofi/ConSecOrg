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
[Route("api/v1/groupchats")]
[Authorize]
public class GroupChatController(AppDbContext db, IHubContext<BoardHub> hub, ChatEncryptionService chatCrypto) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException());

    [HttpGet]
    public async Task<ActionResult<List<GroupChatDto>>> GetMyGroupChats(CancellationToken ct)
    {
        var myId = CurrentUserId;
        var groups = await db.GroupChats
            .AsNoTracking()
            .Where(g => g.CreatedByUserId == myId || g.Members.Any(m => m.UserId == myId))
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);

        return Ok(groups.Select(MapDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GroupChatDto>> GetById(Guid id, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var g = await db.GroupChats
            .AsNoTracking()
            .Include(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (g is null) return NotFound();
        if (g.CreatedByUserId != myId && !g.Members.Any(m => m.UserId == myId)) return Forbid();
        return Ok(MapDto(g));
    }

    [HttpPost]
    public async Task<ActionResult<GroupChatDto>> Create([FromBody] CreateGroupChatDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Название группы обязательно." });

        var myId = CurrentUserId;
        var group = new GroupChat(Guid.NewGuid(), dto.Name.Trim(), myId);
        db.GroupChats.Add(group);

        // Creator is always a member
        db.GroupChatMembers.Add(new GroupChatMember { GroupChatId = group.Id, UserId = myId, JoinedAt = DateTime.UtcNow });

        // Add requested members
        foreach (var memberId in dto.MemberIds.Where(id => id != myId).Distinct())
            db.GroupChatMembers.Add(new GroupChatMember { GroupChatId = group.Id, UserId = memberId, JoinedAt = DateTime.UtcNow });

        await db.SaveChangesAsync(ct);

        // Reload with members for response
        var created = await db.GroupChats.AsNoTracking()
            .Include(g => g.Members).ThenInclude(m => m.User)
            .FirstAsync(g => g.Id == group.Id, ct);

        return CreatedAtAction(nameof(GetById), new { id = group.Id }, MapDto(created));
    }

    public record AddMemberRequest(Guid UserId);

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest body, CancellationToken ct)
    {
        return await AddMemberCore(id, body.UserId, ct);
    }

    private async Task<IActionResult> AddMemberCore(Guid id, Guid userId, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var g = await db.GroupChats.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (g is null) return NotFound();
        if (g.CreatedByUserId != myId) return Forbid();
        if (g.Members.Any(m => m.UserId == userId)) return Ok(); // already member

        db.GroupChatMembers.Add(new GroupChatMember { GroupChatId = id, UserId = userId, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);

        // Notify new member via SignalR
        await hub.Clients.Group(BoardHub.UserGroup(userId))
            .SendAsync("GroupChatMemberAdded", id, ct);

        return Ok();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var g = await db.GroupChats.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (g is null) return NotFound();
        if (g.CreatedByUserId != myId && userId != myId) return Forbid(); // only creator can remove others

        var member = g.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is not null)
        {
            db.GroupChatMembers.Remove(member);
            await db.SaveChangesAsync(ct);
        }
        return Ok();
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(Guid id, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var isMember = await db.GroupChatMembers.AnyAsync(m => m.GroupChatId == id && m.UserId == myId, ct)
                    || await db.GroupChats.AnyAsync(g => g.Id == id && g.CreatedByUserId == myId, ct);
        if (!isMember) return Forbid();

        var messages = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.GroupChatId == id)
            .Include(m => m.Sender)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

        // Latest read timestamp from any OTHER member (for my own messages' read status)
        var chatKey = $"group:{id:N}";
        var latestOtherRead = await db.ChatLastReads.AsNoTracking()
            .Where(r => r.ChatKey == chatKey && r.UserId != myId)
            .MaxAsync(r => (DateTime?)r.LastReadAt, ct);

        return Ok(messages.Select(m =>
        {
            var dto = MapMsgDto(m);
            if (m.SenderUserId == myId && latestOtherRead.HasValue)
                dto.IsReadByRecipient = m.SentAt <= latestOtherRead.Value;
            return dto;
        }).ToList());
    }

    public record RenameGroupChatRequest(string Name);
    public record TransferOwnerRequest(Guid NewOwnerUserId);

    [HttpPatch("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameGroupChatRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name)) return BadRequest(new { message = "Название не может быть пустым." });
        var myId = CurrentUserId;
        var g = await db.GroupChats.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (g is null) return NotFound();
        if (g.CreatedByUserId != myId) return Forbid();
        g.SetName(body.Name.Trim());
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("{id:guid}/transfer-owner")]
    public async Task<IActionResult> TransferOwner(Guid id, [FromBody] TransferOwnerRequest body, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var g = await db.GroupChats.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id, ct);
        if (g is null) return NotFound();
        if (g.CreatedByUserId != myId) return Forbid();
        if (!g.Members.Any(m => m.UserId == body.NewOwnerUserId))
            return BadRequest(new { message = "Пользователь не является участником группы." });
        g.TransferOwnership(body.NewOwnerUserId);
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var myId = CurrentUserId;
        var g = await db.GroupChats.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (g is null) return NotFound();
        if (g.CreatedByUserId != myId) return Forbid();
        db.GroupChats.Remove(g);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static GroupChatDto MapDto(GroupChat g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        CreatedByUserId = g.CreatedByUserId,
        CreatedAt = g.CreatedAt,
        Members = g.Members.Select(m => new GroupChatMemberDto
        {
            UserId = m.UserId,
            Username = m.User?.Username ?? string.Empty
        }).ToList()
    };

    private ChatMessageDto MapMsgDto(ChatMessage m) => new()
    {
        Id = m.Id,
        GroupChatId = m.GroupChatId,
        SenderUserId = m.SenderUserId,
        SenderUsername = m.Sender?.Username ?? string.Empty,
        Text = m.IsTextEncrypted
            ? chatCrypto.Decrypt(m.TextCipher!, m.TextNonce!, m.TextHmac!)
            : m.Text,
        SentAt = m.SentAt,
        AttachmentFileId = m.AttachmentFileId,
        AttachmentFileName = m.AttachmentFileName,
        AttachmentSize = m.AttachmentSize
    };
}
