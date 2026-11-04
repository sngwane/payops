using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Database.Configurations;

public class PaymentBatchConfiguration : IEntityTypeConfiguration<PaymentBatch>
{
    public void Configure(EntityTypeBuilder<PaymentBatch> builder)
    {
        builder.ToTable(nameof(PaymentBatch), t =>
            t.HasCheckConstraint("CK_PaymentBatch_MakerChecker",
                "[ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [CreatedByUserId]"));
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Name).HasMaxLength(255);
        builder.Property(e => e.FileName).HasMaxLength(255);
        builder.Property(e => e.Comment).HasMaxLength(500);
        builder.Property(e => e.BatchStatusId).HasDefaultValue((byte)BatchStatusCode.Created);
        builder.Property(e => e.ApprovalStatusId).HasDefaultValue((byte)ApprovalStatusCode.Pending).HasSentinel((byte)0);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.Reference).IsUnique();
        builder.HasIndex(e => e.RunDate);
        builder.HasOne(e => e.Bank).WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BatchStatus).WithMany().HasForeignKey(e => e.BatchStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ApprovalStatus).WithMany().HasForeignKey(e => e.ApprovalStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ApprovedByUser).WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentSummaryConfiguration : IEntityTypeConfiguration<PaymentSummary>
{
    public void Configure(EntityTypeBuilder<PaymentSummary> builder)
    {
        builder.ToTable(nameof(PaymentSummary), t => t.IsTemporal(h =>
        {
            h.UseHistoryTable("PaymentSummaryHistory");
            h.HasPeriodStart("ValidFrom");
            h.HasPeriodEnd("ValidTo");
        }));
        builder.Property(e => e.RejectionAccountName).HasMaxLength(100);
        builder.Property(e => e.RejectionBranchCode).HasMaxLength(10);
        builder.Property(e => e.RejectionAccountNumber).HasMaxLength(20);
        builder.Property(e => e.UniqueReference).HasMaxLength(100);
        builder.Property(e => e.ReconStatusId).HasDefaultValue((byte)ReconStatusCode.Unreconciled).HasSentinel((byte)0);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.UniqueReference).IsUnique();
        builder.HasIndex(e => new { e.SecondaryClientId, e.SequenceNumber }).IsUnique();
        builder.HasIndex(e => e.PaymentDate);
        builder.HasOne(e => e.PaymentBatch).WithMany(b => b.PaymentSummaries).HasForeignKey(e => e.PaymentBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SecondaryClient).WithMany(c => c.PaymentSummaries).HasForeignKey(e => e.SecondaryClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ServiceType).WithMany().HasForeignKey(e => e.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ReconStatus).WithMany().HasForeignKey(e => e.ReconStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentSummaryCommentConfiguration : IEntityTypeConfiguration<PaymentSummaryComment>
{
    public void Configure(EntityTypeBuilder<PaymentSummaryComment> builder)
    {
        builder.ToTable(nameof(PaymentSummaryComment));
        builder.Property(e => e.Comment).HasMaxLength(1000);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(e => e.PaymentSummary).WithMany(s => s.Comments).HasForeignKey(e => e.PaymentSummaryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable(nameof(PaymentRecord), t => t.HasCheckConstraint("CK_PaymentRecord_Amount", "[Amount] > 0"));
        builder.Property(e => e.BankFileReference).HasMaxLength(100);
        builder.Property(e => e.EmployeeCode).HasMaxLength(50);
        builder.Property(e => e.Surname).HasMaxLength(100);
        builder.Property(e => e.FirstName).HasMaxLength(100);
        builder.Property(e => e.Initials).HasMaxLength(10);
        builder.Property(e => e.IdOrPassport).HasMaxLength(50);
        builder.Property(e => e.Cell).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(255);
        builder.Property(e => e.BranchCode).HasMaxLength(10);
        builder.Property(e => e.AccountNumber).HasMaxLength(20);
        builder.Property(e => e.AccountType).HasMaxLength(10);
        builder.Property(e => e.BankReference).HasMaxLength(100);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.Property(e => e.Comment).HasMaxLength(500);
        builder.Property(e => e.TransactionStatusId).HasDefaultValue((byte)TransactionStatusCode.Pending).HasSentinel((byte)0);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.BankFileReference);
        builder.HasIndex(e => e.TransactionStatusId);
        builder.HasOne(e => e.PaymentSummary).WithMany(s => s.PaymentRecords).HasForeignKey(e => e.PaymentSummaryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TransactionStatus).WithMany().HasForeignKey(e => e.TransactionStatusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentRejectionConfiguration : IEntityTypeConfiguration<PaymentRejection>
{
    public void Configure(EntityTypeBuilder<PaymentRejection> builder)
    {
        builder.ToTable(nameof(PaymentRejection), t => t.HasCheckConstraint("CK_PaymentRejection_Amount", "[Amount] > 0"));
        builder.Property(e => e.AccountName).HasMaxLength(100);
        builder.Property(e => e.BranchCode).HasMaxLength(10);
        builder.Property(e => e.AccountNumber).HasMaxLength(20);
        builder.Property(e => e.BankReference).HasMaxLength(100);
        builder.Property(e => e.BankFileReference).HasMaxLength(100);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.Property(e => e.TransactionStatusId).HasDefaultValue((byte)TransactionStatusCode.PendingUnreconciled).HasSentinel((byte)0);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.PaymentRecordId).IsUnique();
        builder.HasIndex(e => e.TransactionStatusId);
        builder.HasIndex(e => e.BankFileReference);
        builder.HasOne(e => e.PaymentRecord).WithOne(r => r.PaymentRejection).HasForeignKey<PaymentRejection>(e => e.PaymentRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaymentBatch).WithMany().HasForeignKey(e => e.PaymentBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ServiceType).WithMany().HasForeignKey(e => e.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TransactionStatus).WithMany().HasForeignKey(e => e.TransactionStatusId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentRecordStatusHistoryConfiguration : IEntityTypeConfiguration<PaymentRecordStatusHistory>
{
    public void Configure(EntityTypeBuilder<PaymentRecordStatusHistory> builder)
    {
        builder.ToTable(nameof(PaymentRecordStatusHistory));
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ChangedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => new { e.TransactionStatusId, e.ChangedDate });
        builder.HasOne(e => e.PaymentRecord).WithMany(r => r.StatusHistory).HasForeignKey(e => e.PaymentRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaymentRejection).WithMany().HasForeignKey(e => e.PaymentRejectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TransactionStatus).WithMany().HasForeignKey(e => e.TransactionStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaymentBatch).WithMany().HasForeignKey(e => e.PaymentBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ChangedByUser).WithMany().HasForeignKey(e => e.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
