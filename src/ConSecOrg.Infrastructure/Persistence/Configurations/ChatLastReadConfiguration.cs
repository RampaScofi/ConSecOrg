using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class ChatLastReadConfiguration : IEntityTypeConfiguration<ChatLastRead>
{
    public void Configure(EntityTypeBuilder<ChatLastRead> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ChatKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.ChatKey }).IsUnique();
        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
