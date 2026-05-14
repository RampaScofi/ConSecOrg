using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.Text).HasMaxLength(4000).IsRequired();
        builder.Property(m => m.SentAt).HasColumnType("datetime2");

        // ГОСТ encrypted text columns (nullable — absent on old messages)
        builder.Property(m => m.TextCipher)
            .HasColumnName("text_cipher")
            .HasColumnType("VARBINARY(MAX)")
            .IsRequired(false);
        builder.Property(m => m.TextNonce)
            .HasColumnName("text_nonce")
            .HasColumnType("VARBINARY(16)")
            .IsRequired(false);
        builder.Property(m => m.TextHmac)
            .HasColumnName("text_hmac")
            .HasColumnType("VARBINARY(32)")
            .IsRequired(false);

        builder.HasOne(m => m.Sender).WithMany()
            .HasForeignKey(m => m.SenderUserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.GroupChat).WithMany(g => g.Messages)
            .HasForeignKey(m => m.GroupChatId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.SenderUserId, m.ToUserId, m.SentAt });
        builder.HasIndex(m => new { m.ProjectId, m.SentAt });
        builder.HasIndex(m => m.GroupChatId);

        builder.ToTable("chat_messages");
    }
}
