using ConSecOrg.Domain.Enumerations;

namespace ConSecOrg.Domain.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }
    string Username { get; }
    UserRole Role { get; }
    string? IpAddress { get; }
    bool HasPermission(string resource, string action);
    bool IsAuthenticated { get; }
}
