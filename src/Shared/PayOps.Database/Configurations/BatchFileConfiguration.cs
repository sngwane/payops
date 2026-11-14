using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Database.Configurations;

public class BatchUploadConfiguration : IEntityTypeConfiguration<BatchUpload>
{
    public void Configure(EntityTypeBuilder<BatchUpload> builder)
    {
        builder.ToTable(nameof(BatchUpload));
        builder.Property(e => e.UniqueClientId).HasMaxLength(50);
        builder.Property(e => e.UniqueReference).HasMaxLength(100);
        builder.Property(e => e.Format).HasConversion<byte>();
        builder.Property(e => e.PayloadHash).HasMaxLength(32).IsFixedLength();
        builder.Property(e => e.ReceivedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => new { e.SecondaryClientId, e.ReceivedDate });
        builder.HasIndex(e => e.ReceivedDate);
        builder.HasOne(e => e.SecondaryClient).WithMany().HasForeignKey(e => e.SecondaryClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ClientApiKey).WithMany().HasForeignKey(e => e.ClientApiKeyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaymentSummary).WithMany().HasForeignKey(e => e.PaymentSummaryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.ToTable(nameof(PublicHoliday));
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.HolidayDate).IsUnique();
    }
}

public class BillingRateConfiguration : IEntityTypeConfiguration<BillingRate>
{
    public void Configure(EntityTypeBuilder<BillingRate> builder)
    {
        builder.ToTable(nameof(BillingRate), t => t.HasCheckConstraint("CK_BillingRate_FeePerRecord", "[FeePerRecord] >= 0"));
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => new { e.ServiceTypeId, e.EffectiveFrom }).IsUnique();
        builder.HasOne(e => e.ServiceType).WithMany().HasForeignKey(e => e.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BatchContactConfiguration : IEntityTypeConfiguration<BatchContact>
{
    public void Configure(EntityTypeBuilder<BatchContact> builder)
    {
        builder.ToTable(nameof(BatchContact), t => t.HasCheckConstraint(
            "CK_BatchContact_Channel", "[Email] IS NOT NULL OR [CellNumber] IS NOT NULL"));
        builder.Property(e => e.Purpose).HasConversion<byte>();
        builder.Property(e => e.Name).HasMaxLength(50);
        builder.Property(e => e.Email).HasMaxLength(100);
        builder.Property(e => e.CellNumber).HasMaxLength(15);
        builder.HasIndex(e => new { e.PaymentSummaryId, e.Purpose });
        builder.HasOne(e => e.PaymentSummary).WithMany(s => s.Contacts).HasForeignKey(e => e.PaymentSummaryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.ToTable(nameof(NotificationMessage));
        builder.Property(e => e.Type).HasConversion<byte>();
        builder.Property(e => e.Status).HasConversion<byte>().HasDefaultValue(NotificationMessageStatus.Pending).HasSentinel((NotificationMessageStatus)0);
        builder.Property(e => e.DeduplicationKey).HasMaxLength(200);
        builder.Property(e => e.LastError).HasMaxLength(1000);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => e.DeduplicationKey).IsUnique();
        builder.HasIndex(e => new { e.Status, e.NextAttemptDate });
        builder.HasOne(e => e.SecondaryClient).WithMany().HasForeignKey(e => e.SecondaryClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaymentSummary).WithMany().HasForeignKey(e => e.PaymentSummaryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class JobLeaseConfiguration : IEntityTypeConfiguration<JobLease>
{
    public void Configure(EntityTypeBuilder<JobLease> builder)
    {
        builder.ToTable(nameof(JobLease));
        builder.HasKey(e => e.JobName);
        builder.Property(e => e.JobName).HasMaxLength(100);
        builder.Property(e => e.Owner).HasMaxLength(200);
        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}
