using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(36);
        builder.Property(a => a.Status).HasMaxLength(20).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.PreviousHash).HasColumnType("VARBINARY(32)").IsRequired();
        builder.Property(a => a.CurrentHash).HasColumnType("VARBINARY(32)").IsRequired();
        builder.Property(a => a.SequenceNum).ValueGeneratedOnAdd();

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.SequenceNum).IsUnique();

        builder.ToTable("audit_logs");
    }
}
