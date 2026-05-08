using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.OwnsOne(c => c.Name, ec =>
        {
            ec.Property(x => x.CipherText).HasColumnName("name_encrypted").HasColumnType("VARBINARY(256)").IsRequired();
            ec.Property(x => x.Nonce).HasColumnName("name_nonce").HasColumnType("VARBINARY(16)").IsRequired();
            ec.Property(x => x.Hmac).HasColumnName("name_hmac").HasColumnType("VARBINARY(32)").IsRequired();
            ec.Property(x => x.AlgorithmId).HasColumnName("name_algorithm").HasMaxLength(50).IsRequired();
        });

        builder.OwnsOne(c => c.Email, ec =>
        {
            ec.Property(x => x.CipherText).HasColumnName("email_encrypted").HasColumnType("VARBINARY(512)");
            ec.Property(x => x.Nonce).HasColumnName("email_nonce").HasColumnType("VARBINARY(16)");
            ec.Property(x => x.Hmac).HasColumnName("email_hmac").HasColumnType("VARBINARY(32)");
            ec.Property(x => x.AlgorithmId).HasColumnName("email_algorithm").HasMaxLength(50);
        });

        builder.OwnsOne(c => c.Phone, ec =>
        {
            ec.Property(x => x.CipherText).HasColumnName("phone_encrypted").HasColumnType("VARBINARY(128)");
            ec.Property(x => x.Nonce).HasColumnName("phone_nonce").HasColumnType("VARBINARY(16)");
            ec.Property(x => x.Hmac).HasColumnName("phone_hmac").HasColumnType("VARBINARY(32)");
            ec.Property(x => x.AlgorithmId).HasColumnName("phone_algorithm").HasMaxLength(50);
        });

        builder.OwnsOne(c => c.Notes, ec =>
        {
            ec.Property(x => x.CipherText).HasColumnName("notes_encrypted").HasColumnType("VARBINARY(MAX)");
            ec.Property(x => x.Nonce).HasColumnName("notes_nonce").HasColumnType("VARBINARY(16)");
            ec.Property(x => x.Hmac).HasColumnName("notes_hmac").HasColumnType("VARBINARY(32)");
            ec.Property(x => x.AlgorithmId).HasColumnName("notes_algorithm").HasMaxLength(50);
        });

        builder.Property(c => c.LinkedUserId).HasColumnName("linked_user_id").IsRequired(false);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("contacts");
    }
}
