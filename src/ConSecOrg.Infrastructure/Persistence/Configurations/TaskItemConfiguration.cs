using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Priority).HasConversion<byte>();

        builder.OwnsOne(t => t.Description, ec =>
        {
            ec.Property(x => x.CipherText).HasColumnName("description_encrypted").HasColumnType("VARBINARY(MAX)");
            ec.Property(x => x.Nonce).HasColumnName("description_nonce").HasColumnType("VARBINARY(16)");
            ec.Property(x => x.Hmac).HasColumnName("description_hmac").HasColumnType("VARBINARY(32)");
            ec.Property(x => x.AlgorithmId).HasColumnName("description_algorithm").HasMaxLength(50);
        });

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("task_items");
    }
}
