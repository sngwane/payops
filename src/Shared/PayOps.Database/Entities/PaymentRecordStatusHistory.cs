namespace PayOps.Database.Entities;

/// <summary>Audit trail of status changes of a payment record and its rejection refund.</summary>
public class PaymentRecordStatusHistory
{
    public long Id { get; set; }
    public long PaymentRecordId { get; set; }
    public long? PaymentRejectionId { get; set; }
    public byte TransactionStatusId { get; set; }
    public int? PaymentBatchId { get; set; }
    public string? Reason { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTime ChangedDate { get; set; }

    public PaymentRecord PaymentRecord { get; set; } = null!;
    public PaymentRejection? PaymentRejection { get; set; }
    public TransactionStatus TransactionStatus { get; set; } = null!;
    public PaymentBatch? PaymentBatch { get; set; }
    public AppUser? ChangedByUser { get; set; }
}
