using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class Session : BaseEntity<Guid>
{
    public Guid UserId { get; private set; }
    public byte[] TokenHash { get; private set; } = [];  // Streebog-256 of refresh token
    public string IpAddress { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public string? DeviceId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public User? User { get; private set; }

    protected Session() { }

    public Session(Guid id, Guid userId, byte[] tokenHash, string ipAddress,
        DateTime expiresAt, string? userAgent = null, string? deviceId = null) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        IpAddress = ipAddress;
        ExpiresAt = expiresAt;
        UserAgent = userAgent;
        DeviceId = deviceId;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
}
