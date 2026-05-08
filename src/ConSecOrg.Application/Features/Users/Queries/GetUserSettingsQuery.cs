using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Shared.DTOs.Users;
using MediatR;

namespace ConSecOrg.Application.Features.Users.Queries;

public record GetUserSettingsQuery(Guid UserId) : IRequest<UserSettingsDto>;

public class GetUserSettingsQueryHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<GetUserSettingsQuery, UserSettingsDto>
{
    public async Task<UserSettingsDto> Handle(GetUserSettingsQuery query, CancellationToken ct)
    {
        if (query.UserId != currentUser.UserId && currentUser.Role != Domain.Enumerations.UserRole.Admin)
            throw new ForbiddenException();

        var user = await uow.Users.GetByIdAsync(query.UserId, ct)
            ?? throw new NotFoundException("User", query.UserId);

        if (user.Settings is null)
            return new UserSettingsDto { UserId = query.UserId };

        return new UserSettingsDto
        {
            UserId = query.UserId,
            Theme = user.Settings.Theme,
            AccentColor = user.Settings.AccentColor,
            FontSize = user.Settings.FontSize,
            LayoutSettings = user.Settings.LayoutSettingsJson
        };
    }
}
