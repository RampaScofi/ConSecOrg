using ConSecOrg.Application.Features.Auth.Commands;
using ConSecOrg.Application.Features.Auth.Queries;
using ConSecOrg.Shared.DTOs.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var result = await sender.Send(new LoginCommand(dto.Username, dto.Password, ip, ua, dto.DeviceFingerprint), ct);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponseDto>> Refresh([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        var result = await sender.Send(new RefreshTokenCommand(dto.RefreshToken, ip, ua), ct);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sessionId = Guid.Parse(User.FindFirst("session_id")!.Value);
        await sender.Send(new LogoutCommand(sessionId), ct);
        return NoContent();
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Guid>> Register([FromBody] RegisterRequestDto dto, CancellationToken ct)
    {
        var id = await sender.Send(new RegisterCommand(dto.Username, dto.Email, dto.Password, Guid.Parse(dto.RoleId)), ct);
        return CreatedAtAction(nameof(Me), new { }, id);
    }

    /// <summary>Bootstrap: creates the first admin user if no users exist yet.</summary>
    [HttpPost("bootstrap")]
    [AllowAnonymous]
    public async Task<ActionResult<Guid>> Bootstrap([FromBody] RegisterRequestDto dto, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<ConSecOrg.Infrastructure.Persistence.AppDbContext>();
        if (await db.Users.AnyAsync(ct))
            return Conflict("Users already exist. Use the admin account to register more users.");
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", ct);
        if (adminRole is null) return BadRequest("Roles not seeded.");
        var id = await sender.Send(new RegisterCommand(dto.Username, dto.Email, dto.Password, adminRole.Id), ct);
        return Ok(id);
    }

    /// <summary>Self-registration: any unauthenticated user can create a User-role account.</summary>
    [HttpPost("register-self")]
    [AllowAnonymous]
    public async Task<ActionResult<Guid>> RegisterSelf([FromBody] RegisterRequestDto dto, CancellationToken ct)
    {
        var db = HttpContext.RequestServices.GetRequiredService<ConSecOrg.Infrastructure.Persistence.AppDbContext>();
        var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "User", ct);
        if (userRole is null) return BadRequest("Roles not seeded.");
        var id = await sender.Send(new RegisterCommand(dto.Username, dto.Email, dto.Password, userRole.Id), ct);
        return CreatedAtAction(nameof(Me), new { }, id);
    }

    [HttpPost("change-email")]
    [Authorize]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequestDto dto, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var db = HttpContext.RequestServices.GetRequiredService<ConSecOrg.Infrastructure.Persistence.AppDbContext>();
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();
        if (await db.Users.AnyAsync(u => u.Email == dto.NewEmail && u.Id != userId, ct))
            return Conflict("Email already in use.");
        user.ChangeEmail(dto.NewEmail);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        await sender.Send(new ChangePasswordCommand(userId, dto.CurrentPassword, dto.NewPassword), ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfoDto>> Me(CancellationToken ct)
    {
        var userId = Guid.Parse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User ID claim missing."));
        var result = await sender.Send(new GetCurrentUserQuery(userId), ct);
        return Ok(result);
    }
}
