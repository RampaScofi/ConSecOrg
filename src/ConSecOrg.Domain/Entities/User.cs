using ConSecOrg.Domain.Common;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Entities;

public class User : AuditableEntity<Guid>
{
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public byte[] PasswordHash { get; private set; } = [];
    public byte[] Salt { get; private set; } = [];
    public Guid RoleId { get; private set; }
    public Role? Role { get; private set; }
    public string? DeviceId { get; private set; }
    public bool IsLocked { get; private set; }
    public int FailedAttempts { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? LastLoginIp { get; private set; }
    public string? AvatarBase64 { get; private set; }

    public UserSettings? Settings { get; private set; }

    private readonly List<Note> _notes = [];
    public IReadOnlyCollection<Note> Notes => _notes.AsReadOnly();

    private readonly List<Session> _sessions = [];
    public IReadOnlyCollection<Session> Sessions => _sessions.AsReadOnly();

    protected User() { }

    public User(Guid id, string username, string email, byte[] passwordHash, byte[] salt, Guid roleId) : base(id)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Salt = salt;
        RoleId = roleId;
    }

    public void RecordLoginSuccess(string ip)
    {
        FailedAttempts = 0;
        LastLoginAt = DateTime.UtcNow;
        LastLoginIp = ip;
    }

    public void RecordLoginFailure()
    {
        FailedAttempts++;
        if (FailedAttempts >= 5)
            IsLocked = true;
    }

    public void Lock() => IsLocked = true;

    public void Unlock()
    {
        IsLocked = false;
        FailedAttempts = 0;
    }

    public void SetDeviceId(DeviceFingerprint fingerprint) => DeviceId = fingerprint.Value;

    public void ChangePassword(byte[] newHash, byte[] newSalt)
    {
        PasswordHash = newHash;
        Salt = newSalt;
    }

    public void AssignRole(Guid roleId) => RoleId = roleId;

    public void ChangeEmail(string newEmail) => Email = newEmail;

    public void SetAvatar(string? base64) => AvatarBase64 = base64;
}
