using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayOps.Database.Entities;

namespace PayOps.Database.Configurations;

public class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable(nameof(Bank));
        builder.Property(e => e.Code).HasMaxLength(20);
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.HasIndex(e => e.Code).IsUnique();
    }
}

public class BankClientProfileConfiguration : IEntityTypeConfiguration<BankClientProfile>
{
    public void Configure(EntityTypeBuilder<BankClientProfile> builder)
    {
        builder.ToTable(nameof(BankClientProfile));
        builder.Property(e => e.ClientProfileNumber).HasMaxLength(20);
        builder.Property(e => e.ClientIdentifier).HasMaxLength(10);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.HasIndex(e => new { e.BankId, e.ClientProfileNumber }).IsUnique();
        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
    }
}
