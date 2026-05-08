using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Shared.DTOs.Auth;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Auth.Queries;

public record GetCurrentUserQuery(Guid UserId) : IRequest<UserInfoDto>;

public class GetCurrentUserQueryHandler(IUnitOfWork uow) : IRequestHandler<GetCurrentUserQuery, UserInfoDto>
{
    public async Task<UserInfoDto> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await uow.Users.GetByIdAsync(query.UserId, ct)
            ?? throw new NotFoundException("User", query.UserId);

        var role = user.Role?.Name switch
        {
            "Admin" => UserRoleDto.Admin,
            "Manager" => UserRoleDto.Manager,
            "Auditor" => UserRoleDto.Auditor,
            _ => UserRoleDto.User
        };

        return new UserInfoDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = role,
            LastLoginAt = user.LastLoginAt,
            LastLoginIp = user.LastLoginIp,
            IsLocked = user.IsLocked
        };
    }
}
