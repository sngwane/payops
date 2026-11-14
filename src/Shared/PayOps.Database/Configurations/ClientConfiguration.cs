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
        builder.Property(e => e.RowVersion).IsRowVersion();
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
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.UniqueClientId).IsUnique();
        builder.HasOne(e => e.PrimaryClient).WithMany(p => p.SecondaryClients)
            .HasForeignKey(e => e.PrimaryClientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClientApiKeyConfiguration : IEntityTypeConfiguration<ClientApiKey>
{
    public void Configure(EntityTypeBuilder<ClientApiKey> builder)
    {
        builder.ToTable(nameof(ClientApiKey), t => t.HasCheckConstraint(
            "CK_ClientApiKey_Owner",
            "([PrimaryClientId] IS NULL AND [SecondaryClientId] IS NOT NULL) OR ([PrimaryClientId] IS NOT NULL AND [SecondaryClientId] IS NULL)"));
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.KeyPrefix).HasMaxLength(16);
        builder.Property(e => e.KeyHash).HasMaxLength(32).IsFixedLength();
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.KeyHash).IsUnique();
        builder.HasOne(e => e.PrimaryClient).WithMany(p => p.ApiKeys)
            .HasForeignKey(e => e.PrimaryClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SecondaryClient).WithMany(s => s.ApiKeys)
            .HasForeignKey(e => e.SecondaryClientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClientBankAccountConfiguration : IEntityTypeConfiguration<ClientBankAccount>
{
    public void Configure(EntityTypeBuilder<ClientBankAccount> builder)
    {
        builder.ToTable(nameof(ClientBankAccount));
        builder.Property(e => e.AccountName).HasMaxLength(100);
        builder.Property(e => e.BranchCode).HasMaxLength(10);
        builder.Property(e => e.AccountNumber).HasMaxLength(20);
        builder.Property(e => e.GlAccountNumber).HasMaxLength(20);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => new { e.PrimaryClientId, e.AccountTypeId }).IsUnique().HasFilter("[IsActive] = 1");
        builder.HasOne(e => e.PrimaryClient).WithMany(p => p.BankAccounts)
            .HasForeignKey(e => e.PrimaryClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AccountType).WithMany()
            .HasForeignKey(e => e.AccountTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
