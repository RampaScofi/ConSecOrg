using ConSecOrg.Application.Features.Auth.Commands;
using ConSecOrg.Application.Features.Users.Commands;
using ConSecOrg.Application.Features.Users.Queries;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Shared.DTOs.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct)
        => Ok(await sender.Send(new GetUsersQuery(), ct));

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<UserSearchDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<UserSearchDto>());
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var currentUserId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID claim missing."));
        var lower = q.ToLower();
        var users = await db.Users
            .Where(u => !u.IsLocked && u.Id != currentUserId &&
                        (u.Username.ToLower().Contains(lower) || u.Email.ToLower().Contains(lower)))
            .Take(20)
            .Select(u => new UserSearchDto { Id = u.Id, Username = u.Username, Email = u.Email, AvatarBase64 = u.AvatarBase64 })
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpGet("{id:guid}/avatar")]
    public async Task<IActionResult> GetAvatar(Guid id, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var avatar = await db.Users
            .Where(u => u.Id == id)
            .Select(u => u.AvatarBase64)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(avatar)) return NotFound();
        return Ok(new { avatarBase64 = avatar });
    }

    [HttpPut("{id:guid}/avatar")]
    public async Task<IActionResult> UpdateAvatar(Guid id, [FromBody] UpdateAvatarRequestDto dto, CancellationToken ct)
    {
        var callerId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());
        if (callerId != id && !User.IsInRole("Admin")) return Forbid();
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();
        user.SetAvatar(dto.AvatarBase64);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] UpdateSettingsRequestDto dto, CancellationToken ct)
    {
        await sender.Send(new UpdateUserSettingsCommand(id, dto.Theme, dto.AccentColor, dto.FontSize, dto.LayoutSettings), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/settings")]
    public async Task<ActionResult<UserSettingsDto>> GetSettings(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetUserSettingsQuery(id), ct));

    [HttpPost("{id:guid}/lock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Lock(Guid id, CancellationToken ct)
    {
        await sender.Send(new LockUserCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unlock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        await sender.Send(new UnlockUserCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequestDto dto, CancellationToken ct)
    {
        await sender.Send(new AssignRoleCommand(id, dto.RoleId), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetSessions(Guid id, CancellationToken ct)
        => Ok(await sender.Send(new GetUserSessionsQuery(id), ct));

    [HttpDelete("{id:guid}/sessions/{sid:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, Guid sid, CancellationToken ct)
    {
        await sender.Send(new LogoutCommand(sid), ct);
        return NoContent();
    }
}
