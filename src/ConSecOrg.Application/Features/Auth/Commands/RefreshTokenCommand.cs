using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Auth;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress, string? UserAgent) : IRequest<LoginResponseDto>;

public class RefreshTokenCommandHandler(
    IUnitOfWork uow,
    IJwtTokenService jwtService,
    ICryptoService cryptoService,
    IEncryptionKeyStore keyStore) : IRequestHandler<RefreshTokenCommand, LoginResponseDto>
{
    public async Task<LoginResponseDto> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var tokenHash = cryptoService.Hash256(System.Text.Encoding.UTF8.GetBytes(cmd.RefreshToken));
        var session = await uow.Users.GetSessionByTokenHashAsync(tokenHash, ct)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (session.IsExpired)
            throw new UnauthorizedAccessException("Refresh token has expired.");

        var user = await uow.Users.GetByIdAsync(session.UserId, ct)
            ?? throw new NotFoundException("User", session.UserId);

        if (user.IsLocked)
            throw new AccountLockedException();

        // Удалить старую сессию
        await uow.Users.DeleteSessionAsync(session.Id, ct);
        keyStore.Remove(session.Id);

        // Создать новую сессию
        var newSessionId = Guid.NewGuid();
        var newRawToken = jwtService.GenerateRefreshToken();
        var newTokenHash = cryptoService.Hash256(System.Text.Encoding.UTF8.GetBytes(newRawToken));

        var newSession = new Session(newSessionId, user.Id, newTokenHash,
            cmd.IpAddress ?? "unknown",
            DateTime.UtcNow.AddDays(7),
            cmd.UserAgent,
            session.DeviceId,
            session.KeyMaterial);  // carry key forward from DB — works even after server restart

        await uow.Users.AddSessionAsync(newSession, ct);
        await uow.SaveChangesAsync(ct);

        // Refresh in-memory cache too
        if (session.KeyMaterial is not null)
            keyStore.Store(newSessionId, session.KeyMaterial);

        var accessToken = jwtService.GenerateAccessToken(user, newSessionId);

        var role = user.Role?.Name switch
        {
            "Admin" => UserRoleDto.Admin,
            "Manager" => UserRoleDto.Manager,
            "Auditor" => UserRoleDto.Auditor,
            _ => UserRoleDto.User
        };

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRawToken,
            AccessTokenExpiry = DateTime.UtcNow.AddMinutes(60),
            User = new UserInfoDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = role,
                LastLoginAt = user.LastLoginAt,
                LastLoginIp = user.LastLoginIp,
                IsLocked = user.IsLocked
            }
        };
    }
}
