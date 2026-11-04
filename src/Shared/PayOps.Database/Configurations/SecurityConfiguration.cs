using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayOps.Database.Entities;

namespace PayOps.Database.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable(nameof(AppUser));
        builder.Property(e => e.Username).HasMaxLength(100);
        builder.Property(e => e.Email).HasMaxLength(255);
        builder.Property(e => e.FirstName).HasMaxLength(100);
        builder.Property(e => e.LastName).HasMaxLength(100);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.ExternalId).IsUnique();
        builder.HasIndex(e => e.Username).IsUnique();
        builder.HasIndex(e => e.Email).IsUnique();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable(nameof(Role));
        builder.Property(e => e.Name).HasMaxLength(50);
        builder.Property(e => e.Description).HasMaxLength(255);
        builder.HasIndex(e => e.Name).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable(nameof(UserRole));
        builder.Property(e => e.AssignedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => new { e.AppUserId, e.RoleId }).IsUnique();
        builder.HasOne(e => e.AppUser).WithMany(u => u.UserRoles).HasForeignKey(e => e.AppUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Role).WithMany(r => r.UserRoles).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
