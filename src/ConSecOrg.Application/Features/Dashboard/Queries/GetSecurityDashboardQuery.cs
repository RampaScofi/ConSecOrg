using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Shared.DTOs.Audit;
using ConSecOrg.Shared.DTOs.Dashboard;
using MediatR;

namespace ConSecOrg.Application.Features.Dashboard.Queries;

public record GetSecurityDashboardQuery : IRequest<SecurityDashboardDto>;

public class GetSecurityDashboardQueryHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<GetSecurityDashboardQuery, SecurityDashboardDto>
{
    public async Task<SecurityDashboardDto> Handle(GetSecurityDashboardQuery query, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(currentUser.UserId, ct)
            ?? throw new NotFoundException("User", currentUser.UserId);

        var activeSessions = await uow.Users.GetActiveSessionsAsync(currentUser.UserId, ct);
        var notes = await uow.Notes.GetByUserAsync(currentUser.UserId, ct);
        var recentLogs = await uow.AuditLogs.GetPagedAsync(1, 10, currentUser.UserId, ct: ct);

        var notesByLevel = new NotesByLevelDto
        {
            Public = notes.Count(n => n.SecurityLevel == SecurityLevel.Public),
            Internal = notes.Count(n => n.SecurityLevel == SecurityLevel.Internal),
            Confidential = notes.Count(n => n.SecurityLevel == SecurityLevel.Confidential),
            Secret = notes.Count(n => n.SecurityLevel == SecurityLevel.Secret)
        };

        var recentEvents = recentLogs.Select(l => new AuditLogDto
        {
            Id = l.Id,
            UserId = l.UserId,
            Action = l.Action.ToString(),
            EntityType = l.EntityType,
            Timestamp = l.Timestamp,
            Status = l.Status,
            IpAddress = l.IpAddress,
            SequenceNum = l.SequenceNum
        }).ToList();

        return new SecurityDashboardDto
        {
            LastLoginAt = user.LastLoginAt,
            LastLoginIp = user.LastLoginIp,
            ActiveSessionsCount = activeSessions.Count,
            NotesByLevel = notesByLevel,
            RecentAuditEvents = recentEvents
        };
    }
}
