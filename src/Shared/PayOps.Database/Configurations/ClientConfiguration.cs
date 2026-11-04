using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayOps.Database.Entities;

namespace PayOps.Database.Configurations;

public class PrimaryClientConfiguration : IEntityTypeConfiguration<PrimaryClient>
{
    public void Configure(EntityTypeBuilder<PrimaryClient> builder)
    {
        builder.ToTable(nameof(PrimaryClient));
        builder.Property(e => e.UniqueClientId).HasMaxLength(20);
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.LogoFileName).HasMaxLength(255);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.UniqueClientId).IsUnique();
    }
}

public class SecondaryClientConfiguration : IEntityTypeConfiguration<SecondaryClient>
{
    public void Configure(EntityTypeBuilder<SecondaryClient> builder)
    {
        builder.ToTable(nameof(SecondaryClient));
        builder.Property(e => e.UniqueClientId).HasMaxLength(20);
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.UniqueClientId).IsUnique();
        builder.HasOne(e => e.PrimaryClient).WithMany(p => p.SecondaryClients)
            .HasForeignKey(e => e.PrimaryClientId).OnDelete(DeleteBehavior.Restrict);
    }
}
