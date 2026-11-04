namespace PayOps.Database.Entities;

/// <summary>
/// Refund of a bank-rejected payment record into the client's rejection account.
/// Lifecycle: PendingUnreconciled -> Pending -> Transmitted (in a later bank run) -> Success / Rejected.
/// </summary>
public class PaymentRejection
{
    public long Id { get; set; }
    public long PaymentRecordId { get; set; }
    public int? PaymentBatchId { get; set; }
    public byte? ServiceTypeId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? BankReference { get; set; }
    public string? BankFileReference { get; set; }
    public byte TransactionStatusId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public PaymentRecord PaymentRecord { get; set; } = null!;
    public PaymentBatch? PaymentBatch { get; set; }
    public ServiceType? ServiceType { get; set; }
    public TransactionStatus TransactionStatus { get; set; } = null!;
}
