using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Application.Features.Auth.Commands;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;
using FluentAssertions;
using Moq;

namespace ConSecOrg.Application.Tests.Auth;

public class LoginCommandHandlerTests
{
    // ── fixtures ───────────────────────────────────────────────────────────────

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IAuditLogRepository> _auditRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Mock<ICryptoService> _crypto = new();
    private readonly Mock<IEncryptionKeyStore> _keyStore = new();
    private readonly Mock<IDeviceService> _device = new();

    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _uow.Setup(u => u.Users).Returns(_userRepo.Object);
        _uow.Setup(u => u.AuditLogs).Returns(_auditRepo.Object);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _auditRepo.Setup(a => a.GetLastAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync((AuditLog?)null);
        _auditRepo.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

        _crypto.Setup(c => c.Hash256(It.IsAny<byte[]>())).Returns(new byte[32]);
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh_token_value");
        _jwt.Setup(j => j.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns("access_token_value");

        _sut = new LoginCommandHandler(
            _uow.Object, _hasher.Object, _jwt.Object,
            _crypto.Object, _keyStore.Object, _device.Object);
    }

    private static User BuildActiveUser(string username = "testuser")
    {
        var user = new User(Guid.NewGuid(), username, $"{username}@test.com",
            new byte[32], new byte[32], Guid.NewGuid());
        return user;
    }

    private static User BuildLockedUser()
    {
        var user = BuildActiveUser("lockeduser");
        // Lock the user by simulating 5 failed attempts
        for (int i = 0; i < 5; i++) user.RecordLoginFailure();
        return user;
    }

    // ── success path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        var user = BuildActiveUser();
        var keyMaterial = new byte[64];
        Random.Shared.NextBytes(keyMaterial);

        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("correctpassword", user.PasswordHash, user.Salt))
               .Returns((true, keyMaterial));
        _userRepo.Setup(r => r.AddSessionAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var cmd = new LoginCommand("testuser", "correctpassword", "127.0.0.1", null, null);
        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.AccessToken.Should().Be("access_token_value");
        result.RefreshToken.Should().Be("refresh_token_value");
        result.User.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task Handle_ValidCredentials_StoresEncryptionKey()
    {
        var user = BuildActiveUser();
        var keyMaterial = new byte[64];

        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((true, keyMaterial));
        _userRepo.Setup(r => r.AddSessionAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var cmd = new LoginCommand("testuser", "password", "127.0.0.1", null, null);
        await _sut.Handle(cmd, CancellationToken.None);

        _keyStore.Verify(k => k.Store(It.IsAny<Guid>(), keyMaterial), Times.Once);
    }

    // ── not found ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UnknownUsername_ThrowsNotFoundException()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("ghost", It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User?)null);

        var cmd = new LoginCommand("ghost", "anypassword", null, null, null);
        var act = () => _sut.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── locked account ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_LockedUser_ThrowsAccountLockedException()
    {
        var locked = BuildLockedUser();
        _userRepo.Setup(r => r.GetByUsernameAsync("lockeduser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(locked);

        var cmd = new LoginCommand("lockeduser", "any", null, null, null);
        var act = () => _sut.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedException>();
    }

    // ── wrong password ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorized()
    {
        var user = BuildActiveUser();
        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((false, Array.Empty<byte>()));

        var cmd = new LoginCommand("testuser", "wrongpassword", null, null, null);
        var act = () => _sut.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WrongPassword_IncrementsFailedAttempts()
    {
        var user = BuildActiveUser();
        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((false, Array.Empty<byte>()));

        var cmd = new LoginCommand("testuser", "bad", null, null, null);
        try { await _sut.Handle(cmd, CancellationToken.None); } catch { }

        user.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Handle_FiveConsecutiveFailures_LocksAccount()
    {
        var user = BuildActiveUser();
        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((false, Array.Empty<byte>()));

        var cmd = new LoginCommand("testuser", "bad", null, null, null);
        for (int i = 0; i < 5; i++)
            try { await _sut.Handle(cmd, CancellationToken.None); } catch { }

        user.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FifthFailure_WritesAccountLockedAudit()
    {
        var user = BuildActiveUser();
        // Pre-set 4 failures so next attempt triggers lock
        for (int i = 0; i < 4; i++) user.RecordLoginFailure();

        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((false, Array.Empty<byte>()));

        var cmd = new LoginCommand("testuser", "bad", null, null, null);
        try { await _sut.Handle(cmd, CancellationToken.None); } catch { }

        // Verify an AccountLocked audit entry was written
        _auditRepo.Verify(a => a.AddAsync(
            It.Is<AuditLog>(l => l.Action == AuditAction.AccountLocked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── successful login resets counter ────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidLogin_ResetsFailedAttempts()
    {
        var user = BuildActiveUser();
        // 3 prior failures
        for (int i = 0; i < 3; i++) user.RecordLoginFailure();
        user.FailedAttempts.Should().Be(3);

        var keyMaterial = new byte[64];
        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((true, keyMaterial));
        _userRepo.Setup(r => r.AddSessionAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var cmd = new LoginCommand("testuser", "correct", "10.0.0.1", null, null);
        await _sut.Handle(cmd, CancellationToken.None);

        user.FailedAttempts.Should().Be(0);
        user.LastLoginIp.Should().Be("10.0.0.1");
    }
}
