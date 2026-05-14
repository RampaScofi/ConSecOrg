using ConSecOrg.Domain.Common;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Entities;

public class AuditLog : BaseEntity<Guid>
{
    public Guid? UserId { get; private set; }
    public AuditAction Action { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Status { get; private set; } = "Success";
    public string? IpAddress { get; private set; }
    public byte[] PreviousHash { get; private set; } = [];
    public byte[] CurrentHash { get; private set; } = [];
    public string? DetailsJson { get; private set; }
    public long SequenceNum { get; private set; }

    protected AuditLog() { }

    public AuditLog(Guid id, Guid? userId, AuditAction action, string entityType,
        string? entityId, string status, string? ipAddress,
        HashChainEntry chainEntry, DateTime timestamp, string? detailsJson = null) : base(id)
    {
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Timestamp = timestamp;
        Status = status;
        IpAddress = ipAddress;
        PreviousHash = chainEntry.PreviousHash;
        CurrentHash = chainEntry.CurrentHash;
        DetailsJson = detailsJson;
    }
}
