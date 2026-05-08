using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Shared.DTOs.Users;
using MediatR;

namespace ConSecOrg.Application.Features.Users.Queries;

public record GetUserSessionsQuery(Guid UserId) : IRequest<IReadOnlyList<SessionDto>>;

public class GetUserSessionsQueryHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<GetUserSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<IReadOnlyList<SessionDto>> Handle(GetUserSessionsQuery query, CancellationToken ct)
    {
        if (query.UserId != currentUser.UserId && currentUser.Role != Domain.Enumerations.UserRole.Admin)
            throw new ForbiddenException();

        var sessions = await uow.Users.GetActiveSessionsAsync(query.UserId, ct);
        return sessions.Select(s => new SessionDto
        {
            Id = s.Id,
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            DeviceId = s.DeviceId,
            ExpiresAt = s.ExpiresAt,
            CreatedAt = s.CreatedAt
        }).ToList();
    }
}
