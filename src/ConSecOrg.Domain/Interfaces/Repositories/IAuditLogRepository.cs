using ConSecOrg.Domain.Entities;

namespace ConSecOrg.Domain.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetLastAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetPagedAsync(int page, int pageSize,
        Guid? userId = null, DateTime? from = null, DateTime? to = null,
        string? action = null, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetAllOrderedAsync(CancellationToken ct = default);
    Task AddAsync(AuditLog log, CancellationToken ct = default);
}
