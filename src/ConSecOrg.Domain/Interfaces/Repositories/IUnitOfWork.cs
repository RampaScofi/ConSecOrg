namespace ConSecOrg.Domain.Interfaces.Repositories;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    INoteRepository Notes { get; }
    ITaskRepository Tasks { get; }
    IContactRepository Contacts { get; }
    IAuditLogRepository AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
