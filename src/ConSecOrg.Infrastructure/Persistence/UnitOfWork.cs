using ConSecOrg.Domain.Interfaces.Repositories;
using ConSecOrg.Infrastructure.Persistence.Repositories;

namespace ConSecOrg.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public IUserRepository Users { get; }
    public INoteRepository Notes { get; }
    public ITaskRepository Tasks { get; }
    public IContactRepository Contacts { get; }
    public IAuditLogRepository AuditLogs { get; }

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
        Users = new UserRepository(db);
        Notes = new NoteRepository(db);
        Tasks = new TaskRepository(db);
        Contacts = new ContactRepository(db);
        AuditLogs = new AuditLogRepository(db);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
