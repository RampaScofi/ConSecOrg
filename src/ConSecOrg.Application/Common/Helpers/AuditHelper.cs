using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Domain.Interfaces.Services;
using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Application.Common.Helpers;

public static class AuditHelper
{
    public static async Task WriteAsync(
        IUnitOfWork uow,
        ICryptoService crypto,
        Guid? userId,
        AuditAction action,
        string entityType,
        string? entityId,
        string status,
        string? ipAddress,
        string? details,
        CancellationToken ct)
    {
        var prev = await uow.AuditLogs.GetLastAsync(ct);
        var prevHash = prev?.CurrentHash ?? new byte[32];
        var ts = DateTime.UtcNow;

        var chainInput = BuildInput(prevHash, ts, action.ToString(), entityId ?? "", userId?.ToString() ?? "");
        var currentHash = crypto.Hash256(chainInput);

        var log = new AuditLog(
            Guid.NewGuid(), userId, action, entityType, entityId,
            status, ipAddress, new HashChainEntry(prevHash, currentHash), ts, details);

        await uow.AuditLogs.AddAsync(log, ct);
    }

    private static byte[] BuildInput(byte[] prevHash, DateTime ts, string action, string entityId, string userId)
    {
        var tsBytes = System.Text.Encoding.UTF8.GetBytes(ts.ToString("O"));
        var actionBytes = System.Text.Encoding.UTF8.GetBytes(action);
        var entityBytes = System.Text.Encoding.UTF8.GetBytes(entityId);
        var userBytes = System.Text.Encoding.UTF8.GetBytes(userId);
        return [.. prevHash, .. tsBytes, .. actionBytes, .. entityBytes, .. userBytes];
    }
}
