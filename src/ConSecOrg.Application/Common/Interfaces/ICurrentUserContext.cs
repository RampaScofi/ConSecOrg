using ConSecOrg.Domain.Enumerations;

namespace ConSecOrg.Application.Common.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    Guid SessionId { get; }
    UserRole Role { get; }
    string? IpAddress { get; }
    string? DeviceFingerprint { get; }
    bool HasPermission(string resource, string action);

    /// <summary>
    /// Returns the 32-byte encryption key (first half of 64-byte KDF output).
    /// Throws if session key not found — requires fresh login.
    /// </summary>
    byte[] GetEncryptionKey();
}
