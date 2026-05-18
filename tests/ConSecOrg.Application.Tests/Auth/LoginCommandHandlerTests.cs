using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Application.Features.Auth.Commands;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Exceptions;
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
        return new User(Guid.NewGuid(), username, $"{username}@test.com",
            new byte[32], new byte[32], Guid.NewGuid());
    }

    private static User BuildLockedUser()
    {
        var user = BuildActiveUser("lockeduser");
        for (int i = 0; i < 5; i++) user.RecordLoginFailure();
        return user;
    }

    private void SetupSuccessfulAuth(User user, byte[]? keyMaterial = null)
    {
        keyMaterial ??= new byte[64];
        _userRepo.Setup(r => r.GetByUsernameAsync(user.Username, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((true, keyMaterial));
        _userRepo.Setup(r => r.AddSessionAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
    }

    // ── 1. success path ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        var user = BuildActiveUser();
        SetupSuccessfulAuth(user);

        var result = await _sut.Handle(
            new LoginCommand("testuser", "correctpassword", "127.0.0.1", null, null),
            CancellationToken.None);

        result.AccessToken.Should().Be("access_token_value");
        result.RefreshToken.Should().Be("refresh_token_value");
        result.User.Username.Should().Be("testuser");
    }

    // ── 2. unknown user ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync("ghost", It.IsAny<CancellationToken>()))
                 .ReturnsAsync((User?)null);

        var act = () => _sut.Handle(
            new LoginCommand("ghost", "anypassword", null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── 3. locked account ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_LockedAccount_ThrowsForbidden()
    {
        var locked = BuildLockedUser();
        _userRepo.Setup(r => r.GetByUsernameAsync("lockeduser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(locked);

        var act = () => _sut.Handle(
            new LoginCommand("lockeduser", "any", null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedException>();
    }

    // ── 4. wrong password ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorized()
    {
        var user = BuildActiveUser();
        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((false, Array.Empty<byte>()));

        var act = () => _sut.Handle(
            new LoginCommand("testuser", "wrongpassword", null, null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── 5. failed attempt increments counter ──────────────────────────────────

    [Fact]
    public async Task Handle_FailedAttempt_IncrementsCounter()
    {
        var user = BuildActiveUser();
        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((false, Array.Empty<byte>()));

        try { await _sut.Handle(new LoginCommand("testuser", "bad", null, null, null), CancellationToken.None); } catch { }

        user.FailedAttempts.Should().Be(1);
    }

    // ── 6. fifth failure locks account and writes AccountLocked audit ─────────

    [Fact]
    public async Task Handle_FifthFailedAttempt_LocksAccountAndAuditsAccountLocked()
    {
        var user = BuildActiveUser();
        for (int i = 0; i < 4; i++) user.RecordLoginFailure();

        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((false, Array.Empty<byte>()));

        try { await _sut.Handle(new LoginCommand("testuser", "bad", null, null, null), CancellationToken.None); } catch { }

        user.IsLocked.Should().BeTrue();
        _auditRepo.Verify(a => a.AddAsync(
            It.Is<AuditLog>(l => l.Action == AuditAction.AccountLocked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── 7. successful login resets failed attempts ────────────────────────────

    [Fact]
    public async Task Handle_SuccessfulLogin_ResetsFailedAttempts()
    {
        var user = BuildActiveUser();
        for (int i = 0; i < 3; i++) user.RecordLoginFailure();
        SetupSuccessfulAuth(user);

        await _sut.Handle(
            new LoginCommand("testuser", "correct", "10.0.0.1", null, null),
            CancellationToken.None);

        user.FailedAttempts.Should().Be(0);
        user.LastLoginIp.Should().Be("10.0.0.1");
    }

    // ── 8. device mismatch throws DeviceMismatchException ─────────────────────

    [Fact]
    public async Task Handle_DeviceMismatch_ThrowsForbiddenAndAudits()
    {
        var user = BuildActiveUser();
        user.SetDeviceId(new DeviceFingerprint("registered_device"));

        _userRepo.Setup(r => r.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash, user.Salt))
               .Returns((true, new byte[64]));
        _device.Setup(d => d.GetFingerprint())
               .Returns(new DeviceFingerprint("different_device"));

        var act = () => _sut.Handle(
            new LoginCommand("testuser", "correct", null, null, "some_fingerprint"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DeviceMismatchException>();
    }

    // ── 9. first login registers device fingerprint ───────────────────────────

    [Fact]
    public async Task Handle_FirstLoginRegistersDevice()
    {
        var user = BuildActiveUser();
        user.DeviceId.Should().BeNull();

        SetupSuccessfulAuth(user);
        _device.Setup(d => d.GetFingerprint())
               .Returns(new DeviceFingerprint("new_device_fp"));

        await _sut.Handle(
            new LoginCommand("testuser", "correct", null, null, "some_fingerprint"),
            CancellationToken.None);

        user.DeviceId.Should().Be("new_device_fp");
    }

    // ── 10. successful login writes Login audit entry ─────────────────────────

    [Fact]
    public async Task Handle_SuccessfulLogin_WritesAuditLog()
    {
        var user = BuildActiveUser();
        SetupSuccessfulAuth(user);

        await _sut.Handle(
            new LoginCommand("testuser", "correct", "127.0.0.1", null, null),
            CancellationToken.None);

        _auditRepo.Verify(a => a.AddAsync(
            It.Is<AuditLog>(l => l.Action == AuditAction.Login && l.Status == "Success"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
