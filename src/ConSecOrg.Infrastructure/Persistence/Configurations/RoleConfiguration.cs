using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.Property(r => r.PermissionsJson).HasColumnType("NVARCHAR(MAX)").IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();

        // Seed default roles
        builder.HasData(
            new Role(new Guid("11111111-1111-1111-1111-111111111111"), "Admin",
                """{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u","d"],"audit":["r"],"users":["r","c","u","d"]}"""),
            new Role(new Guid("22222222-2222-2222-2222-222222222222"), "Manager",
                """{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u"],"audit":[],"users":["r"]}"""),
            new Role(new Guid("33333333-3333-3333-3333-333333333333"), "Auditor",
                """{"notes":["r"],"tasks":["r"],"contacts":[],"audit":["r"],"users":["r"]}"""),
            new Role(new Guid("44444444-4444-4444-4444-444444444444"), "User",
                """{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u","d"],"audit":[],"users":[]}""")
        );

        builder.ToTable("roles");
    }
}
