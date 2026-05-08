using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Persistence;
using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/projects")]
[Authorize]
public class SharedProjectsController(AppDbContext db, ICryptoService crypto) : ControllerBase
{
    // Apply pending migrations once per server lifetime — guarantees feature works without manual `ef database update`
    private static volatile bool _schemaMigrated = false;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaMigrated) return;
        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_schemaMigrated) return;
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
                await db.Database.MigrateAsync(ct);
            _schemaMigrated = true;
        }
        finally { _schemaLock.Release(); }
    }

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User ID claim missing."));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SharedProjectDto>>> GetMine(CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var myId = CurrentUserId;
        var projects = await db.SharedProjects
            .AsNoTracking()
            .Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Owner)
            .Where(p => p.OwnerUserId == myId || p.Members.Any(m => m.UserId == myId))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return Ok(projects.Select(MapDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SharedProjectDto>> Create([FromBody] CreateSharedProjectDto dto, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Имя проекта не может быть пустым." });

        var myId = CurrentUserId;
        var project = new SharedProject(Guid.NewGuid(), myId, dto.Name.Trim());
        db.SharedProjects.Add(project);
        await AuditAsync(myId, AuditAction.ProjectCreated, project.Id.ToString(), $"Name: {project.Name}", ct);
        await db.SaveChangesAsync(ct);

        // Reload as detached so MapDto sees Owner and Members (even if empty)
        var created = await db.SharedProjects
            .AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == project.Id, ct);

        // Fallback: if reload didn't find it (extremely unlikely), build dto from in-memory project
        var result = created is not null ? MapDto(created) : new SharedProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            InviteCode = project.InviteCode,
            OwnerUserId = project.OwnerUserId,
            OwnerUsername = string.Empty,
            Members = new List<SharedProjectMemberDto>(),
            CreatedAt = project.CreatedAt
        };

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SharedProjectDto>> Get(Guid id, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var myId = CurrentUserId;
        var project = await db.SharedProjects
            .AsNoTracking()
            .Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == id && (p.OwnerUserId == myId || p.Members.Any(m => m.UserId == myId)), ct);
        if (project is null) return NotFound();
        return Ok(MapDto(project));
    }

    [HttpPost("join")]
    public async Task<ActionResult<SharedProjectDto>> Join([FromBody] JoinSharedProjectDto dto, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (string.IsNullOrWhiteSpace(dto.InviteCode))
            return BadRequest(new { message = "Код приглашения не может быть пустым." });

        var myId = CurrentUserId;
        var code = dto.InviteCode.Trim().ToUpperInvariant();

        // Lookup project without tracking — we only need its Id and OwnerUserId
        var lookup = await db.SharedProjects
            .AsNoTracking()
            .Where(p => p.InviteCode == code)
            .Select(p => new { p.Id, p.OwnerUserId })
            .FirstOrDefaultAsync(ct);

        if (lookup is null)
            return NotFound(new { message = "Проект с таким кодом приглашения не найден." });

        if (lookup.OwnerUserId == myId)
            return Conflict(new { message = "Вы владелец этого проекта." });

        var alreadyMember = await db.SharedProjectMembers
            .AsNoTracking()
            .AnyAsync(m => m.ProjectId == lookup.Id && m.UserId == myId, ct);

        if (alreadyMember)
            return Conflict(new { message = "Вы уже участник этого проекта." });

        // Insert directly via DbSet — no navigation tracking, no parent UPDATE side-effects
        db.SharedProjectMembers.Add(new SharedProjectMember(lookup.Id, myId));
        await AuditAsync(myId, AuditAction.ProjectJoined, lookup.Id.ToString(), $"InviteCode: {code}", ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race condition — another request added us first. That's fine.
        }

        // Reload full project as detached for response
        var fullProject = await db.SharedProjects
            .AsNoTracking()
            .Include(p => p.Members).ThenInclude(m => m.User)
            .Include(p => p.Owner)
            .FirstAsync(p => p.Id == lookup.Id, ct);

        return Ok(MapDto(fullProject));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var myId = CurrentUserId;

        // Use ExecuteDelete so we don't load and track the entire aggregate
        await AuditAsync(myId, AuditAction.ProjectDeleted, id.ToString(), string.Empty, ct);
        await db.SaveChangesAsync(ct);

        var deleted = await db.SharedProjects
            .Where(p => p.Id == id && p.OwnerUserId == myId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0 ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        var myId = CurrentUserId;

        await AuditAsync(myId, AuditAction.ProjectLeft, id.ToString(), string.Empty, ct);
        await db.SaveChangesAsync(ct);

        var deleted = await db.SharedProjectMembers
            .Where(m => m.ProjectId == id && m.UserId == myId)
            .ExecuteDeleteAsync(ct);

        return deleted > 0 ? NoContent() : NotFound();
    }

    private async Task AuditAsync(Guid userId, AuditAction action, string entityId, string details, CancellationToken ct)
    {
        var prev = await db.AuditLogs.OrderByDescending(l => l.SequenceNum).FirstOrDefaultAsync(ct);
        var prevHash = prev?.CurrentHash ?? new byte[32];
        var ts = DateTime.UtcNow;
        var input = System.Text.Encoding.UTF8.GetBytes(
            string.Concat(Convert.ToBase64String(prevHash), ts.ToString("O"), action.ToString(), entityId, userId.ToString()));
        var currentHash = crypto.Hash256(input);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        db.AuditLogs.Add(new AuditLog(Guid.NewGuid(), userId, action, "SharedProject", entityId,
            "Success", ip, new HashChainEntry(prevHash, currentHash), details));
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static SharedProjectDto MapDto(SharedProject p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        InviteCode = p.InviteCode,
        OwnerUserId = p.OwnerUserId,
        OwnerUsername = p.Owner?.Username ?? string.Empty,
        Members = p.Members.Select(m => new SharedProjectMemberDto
        {
            UserId = m.UserId,
            Username = m.User?.Username ?? string.Empty,
            CanEdit = m.CanEdit,
            JoinedAt = m.JoinedAt
        }).ToList(),
        CreatedAt = p.CreatedAt
    };
}
