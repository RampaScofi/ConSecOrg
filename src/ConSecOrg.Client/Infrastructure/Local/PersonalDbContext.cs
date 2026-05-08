using ConSecOrg.Domain.Common;
using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConSecOrg.Client.Infrastructure.Local;

public sealed class PersonalDbContext : DbContext
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=ConSecOrg_Personal;Trusted_Connection=True;",
            sql => sql.EnableRetryOnFailure(3));
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Note>(b =>
        {
            b.HasKey(n => n.Id);
            b.Property(n => n.Id).ValueGeneratedNever();
            b.Property(n => n.Title).HasMaxLength(500).IsRequired();
            b.Property(n => n.SecurityLevel).HasConversion<byte>();
            b.Ignore(n => n.User);
            b.Ignore(n => n.Category);
            b.Ignore(n => n.Tags);
            b.OwnsOne(n => n.Content, ec =>
            {
                ec.Property(x => x.CipherText)
                    .HasColumnName("content_encrypted")
                    .HasColumnType("VARBINARY(MAX)")
                    .IsRequired();
                ec.Property(x => x.Nonce)
                    .HasColumnName("content_nonce")
                    .HasColumnType("VARBINARY(16)")
                    .IsRequired();
                ec.Property(x => x.Hmac)
                    .HasColumnName("content_hmac")
                    .HasColumnType("VARBINARY(32)")
                    .IsRequired();
                ec.Property(x => x.AlgorithmId)
                    .HasColumnName("content_algorithm")
                    .HasMaxLength(50)
                    .IsRequired();
            });
            b.HasIndex(n => n.UserId);
            b.ToTable("notes");
        });

        model.Entity<TaskItem>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).ValueGeneratedNever();
            b.Property(t => t.Title).HasMaxLength(500).IsRequired();
            b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            b.Property(t => t.Priority).HasConversion<byte>();
            b.Ignore(t => t.User);
            b.OwnsOne(t => t.Description, ec =>
            {
                ec.Property(x => x.CipherText).HasColumnName("description_encrypted").HasColumnType("VARBINARY(MAX)");
                ec.Property(x => x.Nonce).HasColumnName("description_nonce").HasColumnType("VARBINARY(16)");
                ec.Property(x => x.Hmac).HasColumnName("description_hmac").HasColumnType("VARBINARY(32)");
                ec.Property(x => x.AlgorithmId).HasColumnName("description_algorithm").HasMaxLength(50);
            });
            b.ToTable("task_items");
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var e in ChangeTracker.Entries<AuditableEntity<Guid>>())
        {
            if (e.State == EntityState.Added) { e.Entity.CreatedAt = now; e.Entity.UpdatedAt = now; }
            else if (e.State == EntityState.Modified) e.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(ct);
    }

    public Task EnsureSchemaAsync() => Database.EnsureCreatedAsync();
}
