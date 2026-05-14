using ConSecOrg.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConSecOrg.Infrastructure.Persistence.Configurations;

public class SharedProjectConfiguration : IEntityTypeConfiguration<SharedProject>
{
    public void Configure(EntityTypeBuilder<SharedProject> builder)
    {
        builder.ToTable("shared_projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.InviteCode).HasMaxLength(20).IsRequired();
        builder.HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Members)
            .WithOne(m => m.Project)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SharedProjectMemberConfiguration : IEntityTypeConfiguration<SharedProjectMember>
{
    public void Configure(EntityTypeBuilder<SharedProjectMember> builder)
    {
        builder.ToTable("shared_project_members");
        builder.HasKey(m => m.Id);
        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.ProjectId, m.UserId }).IsUnique();
    }
}

public class SharedProjectTaskConfiguration : IEntityTypeConfiguration<SharedProjectTask>
{
    public void Configure(EntityTypeBuilder<SharedProjectTask> builder)
    {
        builder.ToTable("shared_project_tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(4000);
        builder.Property(t => t.Status).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Tags).HasMaxLength(2000).HasDefaultValue("[]");
        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(t => t.AssignedUser)
            .WithMany()
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(t => t.ProjectId);
        builder.HasIndex(t => t.ColumnId);
    }
}

public class SharedProjectColumnConfiguration : IEntityTypeConfiguration<SharedProjectColumn>
{
    public void Configure(EntityTypeBuilder<SharedProjectColumn> builder)
    {
        builder.ToTable("shared_project_columns");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Color).HasMaxLength(20).IsRequired();
        builder.HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.ProjectId, c.Order });
    }
}
