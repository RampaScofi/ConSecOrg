using ConSecOrg.Application.Common.Exceptions;
using ConSecOrg.Application.Common.Interfaces;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ConSecOrg.Server.Infrastructure;

public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _http;
    private readonly IEncryptionKeyStore _keyStore;
    private readonly AppDbContext _db;

    public CurrentUserContext(IHttpContextAccessor http, IEncryptionKeyStore keyStore, AppDbContext db)
    {
        _http = http;
        _keyStore = keyStore;
        _db = db;
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
        if (key is not null)
            return key;

        // Memory cache miss (e.g. server restart) — load from DB
        var keyMaterial = _db.Sessions
            .AsNoTracking()
            .Where(s => s.Id == SessionId)
            .Select(s => s.KeyMaterial)
            .FirstOrDefault();

        if (keyMaterial is null)
            throw new ForbiddenException("Session encryption key not found. Please log in again.");

        // Repopulate cache so subsequent calls in this request are fast
        _keyStore.Store(SessionId, keyMaterial);
        return keyMaterial;
    }
}
