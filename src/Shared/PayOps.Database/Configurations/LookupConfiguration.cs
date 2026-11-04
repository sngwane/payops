using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Database.Configurations;

public abstract class LookupConfiguration<TEntity, TCode> : IEntityTypeConfiguration<TEntity>
    where TEntity : LookupEntity, new()
    where TCode : struct, Enum
{
    protected abstract IReadOnlyDictionary<TCode, string> Names { get; }

    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.ToTable(typeof(TEntity).Name);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Code).HasMaxLength(30);
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasData(Names.Select(n => new TEntity
        {
            Id = Convert.ToByte(n.Key),
            Code = n.Key.ToString(),
            Name = n.Value,
            IsActive = true
        }));
    }
}

public class BatchStatusConfiguration : LookupConfiguration<BatchStatus, BatchStatusCode>
{
    protected override IReadOnlyDictionary<BatchStatusCode, string> Names { get; } = new Dictionary<BatchStatusCode, string>
    {
        [BatchStatusCode.Created] = "Created", [BatchStatusCode.Sent] = "Sent", [BatchStatusCode.Ack] = "Acknowledged",
        [BatchStatusCode.Nack] = "Not acknowledged", [BatchStatusCode.Duplicate] = "Duplicate"
    };
}

public class ReconStatusConfiguration : LookupConfiguration<ReconStatus, ReconStatusCode>
{
    protected override IReadOnlyDictionary<ReconStatusCode, string> Names { get; } = new Dictionary<ReconStatusCode, string>
    {
        [ReconStatusCode.Reconciled] = "Reconciled", [ReconStatusCode.Unreconciled] = "Unreconciled",
        [ReconStatusCode.Discrepancies] = "Discrepancies"
    };
}

public class TransactionStatusConfiguration : LookupConfiguration<TransactionStatus, TransactionStatusCode>
{
    protected override IReadOnlyDictionary<TransactionStatusCode, string> Names { get; } = new Dictionary<TransactionStatusCode, string>
    {
        [TransactionStatusCode.Pending] = "Pending", [TransactionStatusCode.Transmitted] = "Transmitted",
        [TransactionStatusCode.Success] = "Success", [TransactionStatusCode.Rejected] = "Rejected",
        [TransactionStatusCode.PendingUnreconciled] = "Pending unreconciled"
    };
}

public class ServiceTypeConfiguration : LookupConfiguration<ServiceType, ServiceTypeCode>
{
    protected override IReadOnlyDictionary<ServiceTypeCode, string> Names { get; } = new Dictionary<ServiceTypeCode, string>
    {
        [ServiceTypeCode.SDV] = "Same Day Value", [ServiceTypeCode.RTL] = "Real Time Payments", [ServiceTypeCode.OneDay] = "One Day",
        [ServiceTypeCode.TwoDay] = "Two Day", [ServiceTypeCode.DW] = "Direct Withdrawal"
    };
}

public class AccountTypeConfiguration : LookupConfiguration<AccountType, AccountTypeCode>
{
    protected override IReadOnlyDictionary<AccountTypeCode, string> Names { get; } = new Dictionary<AccountTypeCode, string>
    {
        [AccountTypeCode.Nominated] = "Nominated Account", [AccountTypeCode.Charges] = "Charges Account"
    };
}

public class ApprovalStatusConfiguration : LookupConfiguration<ApprovalStatus, ApprovalStatusCode>
{
    protected override IReadOnlyDictionary<ApprovalStatusCode, string> Names { get; } = new Dictionary<ApprovalStatusCode, string>
    {
        [ApprovalStatusCode.Pending] = "Pending approval", [ApprovalStatusCode.Approved] = "Approved",
        [ApprovalStatusCode.Rejected] = "Rejected"
    };
}
