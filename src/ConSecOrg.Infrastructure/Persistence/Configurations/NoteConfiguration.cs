using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Title).HasMaxLength(500).IsRequired();
        builder.Property(n => n.SecurityLevel).HasConversion<byte>();

        builder.OwnsOne(n => n.Content, ec =>
        {
            ec.Property(x => x.CipherText).HasColumnName("content_encrypted").HasColumnType("VARBINARY(MAX)").IsRequired();
            ec.Property(x => x.Nonce).HasColumnName("content_nonce").HasColumnType("VARBINARY(16)").IsRequired();
            ec.Property(x => x.Hmac).HasColumnName("content_hmac").HasColumnType("VARBINARY(32)").IsRequired();
            ec.Property(x => x.AlgorithmId).HasColumnName("content_algorithm").HasMaxLength(50).IsRequired();
        });

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notes)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Category)
            .WithMany(c => c.Notes)
            .HasForeignKey(n => n.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(n => n.Tags)
            .WithMany(t => t.Notes)
            .UsingEntity("note_tags");

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => n.ExpiresAt).HasFilter("[ExpiresAt] IS NOT NULL");

        builder.ToTable("notes");
    }
}
