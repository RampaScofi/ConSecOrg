using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Shared.DTOs.Audit;
using ConSecOrg.Shared.Pagination;
using MediatR;

namespace ConSecOrg.Application.Features.Audit.Queries;

public record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 50,
    Guid? UserId = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Action = null) : IRequest<PagedResponse<AuditLogDto>>;

public class GetAuditLogsQueryHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<GetAuditLogsQuery, PagedResponse<AuditLogDto>>
{
    public async Task<PagedResponse<AuditLogDto>> Handle(GetAuditLogsQuery query, CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Auditor)
            throw new ForbiddenException("Access to audit logs requires Admin or Auditor role.");

        var logs = await uow.AuditLogs.GetPagedAsync(query.Page, query.PageSize,
            query.UserId, query.From, query.To, query.Action, ct);

        // Load usernames for the retrieved log entries
        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var usernames = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            var users = await uow.Users.GetByIdsAsync(userIds, ct);
            foreach (var u in users) usernames[u.Id] = u.Username;
        }

        var items = logs.Select(l => new AuditLogDto
        {
            Id = l.Id,
            UserId = l.UserId,
            Username = l.UserId.HasValue && usernames.TryGetValue(l.UserId.Value, out var uname) ? uname : null,
            Action = l.Action.ToString(),
            EntityType = l.EntityType,
            EntityId = l.EntityId,
            Timestamp = l.Timestamp,
            Status = l.Status,
            IpAddress = l.IpAddress,
            Details = l.DetailsJson,
            SequenceNum = l.SequenceNum
        }).ToList();

        // Для пагинации нам нужен общий count — используем текущую страницу как приближение
        return new PagedResponse<AuditLogDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = items.Count < query.PageSize
                ? (query.Page - 1) * query.PageSize + items.Count
                : query.Page * query.PageSize + 1
        };
    }
}
