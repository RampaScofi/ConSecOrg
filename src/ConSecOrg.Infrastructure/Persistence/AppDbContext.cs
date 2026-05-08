using ConSecOrg.Domain.Common;
using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<SharedProject> SharedProjects => Set<SharedProject>();
    public DbSet<SharedProjectMember> SharedProjectMembers => Set<SharedProjectMember>();
    public DbSet<SharedProjectTask> SharedProjectTasks => Set<SharedProjectTask>();
    public DbSet<SharedProjectColumn> SharedProjectColumns => Set<SharedProjectColumn>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity<Guid>>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
