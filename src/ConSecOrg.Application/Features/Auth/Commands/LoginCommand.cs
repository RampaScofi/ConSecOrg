using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Shared.DTOs.Auth;
using ConSecOrg.Shared.Enums;
using MediatR;

namespace ConSecOrg.Application.Features.Auth.Commands;

public record LoginCommand(
    string Username,
    string Password,
    string? IpAddress,
    string? UserAgent,
    string? DeviceFingerprint) : IRequest<LoginResponseDto>;

public class LoginCommandHandler(
    IUnitOfWork uow,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtService,
    ICryptoService cryptoService,
    IEncryptionKeyStore keyStore,
    IDeviceService deviceService) : IRequestHandler<LoginCommand, LoginResponseDto>
{
    public async Task<LoginResponseDto> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await uow.Users.GetByUsernameAsync(cmd.Username, ct)
            ?? throw new NotFoundException("User", cmd.Username);

        if (user.IsLocked)
            throw new AccountLockedException();

        var (isValid, keyMaterial) = passwordHasher.Verify(cmd.Password, user.PasswordHash, user.Salt);

        if (!isValid)
        {
            user.RecordLoginFailure();
            await uow.SaveChangesAsync(ct);

            // если только что заблокировали — записать аудит
            if (user.IsLocked)
                await WriteAuditAsync(user.Id, AuditAction.AccountLocked, "Failure", cmd.IpAddress, ct);
            else
                await WriteAuditAsync(user.Id, AuditAction.Login, "Failure", cmd.IpAddress, ct);

            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        // Привязка к устройству (корпоративный режим)
        if (cmd.DeviceFingerprint is not null)
        {
            var fp = deviceService.GetFingerprint();
            if (user.DeviceId is null)
                user.SetDeviceId(fp);
            else if (user.DeviceId != fp.Value)
                throw new ConSecOrg.Domain.Exceptions.DeviceMismatchException();
        }

        user.RecordLoginSuccess(cmd.IpAddress ?? "unknown");

        var sessionId = Guid.NewGuid();
        var rawRefreshToken = jwtService.GenerateRefreshToken();
        var tokenHash = cryptoService.Hash256(System.Text.Encoding.UTF8.GetBytes(rawRefreshToken));

        var session = new Session(sessionId, user.Id, tokenHash,
            cmd.IpAddress ?? "unknown",
            DateTime.UtcNow.AddDays(7),
            cmd.UserAgent,
            cmd.DeviceFingerprint,
            keyMaterial);  // persisted to DB so key survives server restarts

        await uow.Users.AddSessionAsync(session, ct);
        await uow.SaveChangesAsync(ct);

        // Also cache in memory for fast lookup during this server's lifetime
        keyStore.Store(sessionId, keyMaterial);

        var accessToken = jwtService.GenerateAccessToken(user, sessionId);
        await WriteAuditAsync(user.Id, AuditAction.Login, "Success", cmd.IpAddress, ct);
        await uow.SaveChangesAsync(ct);

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
            RefreshToken = rawRefreshToken,
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

    private async Task WriteAuditAsync(Guid userId, AuditAction action, string status, string? ip, CancellationToken ct)
    {
        var prev = await uow.AuditLogs.GetLastAsync(ct);
        var prevHash = prev?.CurrentHash ?? new byte[32];
        var ts = DateTime.UtcNow;

        var chainInput = BuildChainInput(prevHash, ts, action.ToString(), userId.ToString(), userId.ToString());
        var currentHash = cryptoService.Hash256(chainInput);
        var entry = new Domain.ValueObjects.HashChainEntry(prevHash, currentHash);

        var log = new AuditLog(Guid.NewGuid(), userId, action, "User", userId.ToString(), status, ip, entry);
        await uow.AuditLogs.AddAsync(log, ct);
    }

    private static byte[] BuildChainInput(byte[] prevHash, DateTime ts, string action, string entityId, string userId)
    {
        var tsBytes = System.Text.Encoding.UTF8.GetBytes(ts.ToString("O"));
        var actionBytes = System.Text.Encoding.UTF8.GetBytes(action);
        var entityBytes = System.Text.Encoding.UTF8.GetBytes(entityId);
        var userBytes = System.Text.Encoding.UTF8.GetBytes(userId);
        return [.. prevHash, .. tsBytes, .. actionBytes, .. entityBytes, .. userBytes];
    }
}
