using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ConSecOrg.Server.Infrastructure;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _http;
    private readonly IEncryptionKeyStore _keyStore;

    public CurrentUserContext(IHttpContextAccessor http, IEncryptionKeyStore keyStore)
    {
        _http = http;
        _keyStore = keyStore;
    }

    private ClaimsPrincipal User => _http.HttpContext?.User
        ?? throw new ForbiddenException("No HTTP context.");

    public Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new ForbiddenException("User ID claim missing."));

    public Guid SessionId => Guid.Parse(User.FindFirstValue("session_id")
        ?? throw new ForbiddenException("Session ID claim missing."));

    public UserRole Role => (User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role")) switch
    {
        "Admin" => UserRole.Admin,
        "Manager" => UserRole.Manager,
        "Auditor" => UserRole.Auditor,
        _ => UserRole.User
    };

    public string? IpAddress => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? DeviceFingerprint => _http.HttpContext?.Request.Headers["X-Device-Fingerprint"];

    public bool HasPermission(string resource, string action) => true;

    public byte[] GetEncryptionKey()
    {
        var key = _keyStore.Get(SessionId);
        if (key is null)
            throw new ForbiddenException("Session encryption key not found. Please log in again.");
        return key; // 64 bytes: [0..31] Kuznechik key, [32..63] HMAC key
    }
}
