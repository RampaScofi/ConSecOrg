using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Shared.DTOs.Users;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Users.Queries;

public record GetUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public class GetUsersQueryHandler(IUnitOfWork uow, ICurrentUserContext currentUser)
    : IRequestHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(GetUsersQuery query, CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Manager)
            throw new ForbiddenException();

        var users = await uow.Users.GetAllAsync(ct);
        return users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            Role = u.Role?.Name switch
            {
                "Admin" => UserRoleDto.Admin,
                "Manager" => UserRoleDto.Manager,
                "Auditor" => UserRoleDto.Auditor,
                _ => UserRoleDto.User
            },
            IsLocked = u.IsLocked,
            FailedAttempts = u.FailedAttempts,
            LastLoginAt = u.LastLoginAt,
            LastLoginIp = u.LastLoginIp,
            CreatedAt = u.CreatedAt
        }).ToList();
    }
}
