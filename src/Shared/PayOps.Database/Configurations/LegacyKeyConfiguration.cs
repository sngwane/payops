using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;

namespace PayOps.Database.Configurations;

/// <summary>
/// Legacy BulkPayments keys kept on imported rows so the legacy import can be re-run without creating duplicates.
/// Rows created by PayOps itself leave them null.
/// </summary>
public static class LegacyKeyConfiguration
{
    private const string HasLegacyId = "[LegacyId] IS NOT NULL";

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bank>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<BankClientProfile>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<PrimaryClient>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<SecondaryClient>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        // Legacy PrimaryClientBankAccountType has no key of its own; LegacyId is the legacy BankAccountId.
        modelBuilder.Entity<ClientBankAccount>().HasIndex(e => new { e.PrimaryClientId, e.AccountTypeId, e.LegacyId })
            .IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<PaymentBatch>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<PaymentSummary>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<PaymentSummary>().Property(e => e.LegacyBatchId).HasMaxLength(50);
        modelBuilder.Entity<PaymentSummary>().HasIndex(e => e.LegacyBatchId);
        modelBuilder.Entity<PaymentRecord>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<PaymentRejection>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
        modelBuilder.Entity<PaymentSummaryComment>().HasIndex(e => e.LegacyId).IsUnique().HasFilter(HasLegacyId);
    }
}
