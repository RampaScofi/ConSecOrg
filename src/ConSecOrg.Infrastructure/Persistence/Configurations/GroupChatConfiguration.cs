using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class GroupChatConfiguration : IEntityTypeConfiguration<GroupChat>
{
    public void Configure(EntityTypeBuilder<GroupChat> builder)
    {
        builder.ToTable("group_chats");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.Property(g => g.CreatedByUserId).IsRequired();
        builder.Property(g => g.CreatedAt).IsRequired();

        builder.HasMany(g => g.Members)
            .WithOne(m => m.GroupChat)
            .HasForeignKey(m => m.GroupChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Messages)
            .WithOne(m => m.GroupChat)
            .HasForeignKey(m => m.GroupChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class GroupChatMemberConfiguration : IEntityTypeConfiguration<GroupChatMember>
{
    public void Configure(EntityTypeBuilder<GroupChatMember> builder)
    {
        builder.ToTable("group_chat_members");
        builder.HasKey(m => new { m.GroupChatId, m.UserId });
        builder.Property(m => m.JoinedAt).IsRequired();
    }
}
