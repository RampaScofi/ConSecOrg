using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db) => _db = db;

    public Task<AuditLog?> GetLastAsync(CancellationToken ct = default)
        => _db.AuditLogs.OrderByDescending(a => a.Timestamp).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> GetPagedAsync(int page, int pageSize,
        Guid? userId = null, DateTime? from = null, DateTime? to = null,
        string? action = null, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (userId.HasValue) query = query.Where(a => a.UserId == userId);
        if (from.HasValue) query = query.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(a => a.Timestamp < to.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(action) && Enum.TryParse<ConSecOrg.Domain.Enumerations.AuditAction>(action, true, out var auditAction))
            query = query.Where(a => a.Action == auditAction);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> GetAllOrderedAsync(CancellationToken ct = default)
        => await _db.AuditLogs.OrderBy(a => a.Timestamp).ToListAsync(ct);

    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
        => await _db.AuditLogs.AddAsync(log, ct);
}
