namespace PayOps.Database.Entities;

/// <summary>One client batch submitted by a secondary client. System-versioned (history in PaymentSummaryHistory).</summary>
public class PaymentSummary
{
    public int Id { get; set; }
    public int? PaymentBatchId { get; set; }
    public int SecondaryClientId { get; set; }
    public byte ServiceTypeId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? RejectionAccountName { get; set; }
    public string? RejectionBranchCode { get; set; }
    public string? RejectionAccountNumber { get; set; }
    public long SequenceNumber { get; set; }
    public string UniqueReference { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public decimal TotalValue { get; set; }
    public byte ReconStatusId { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public PaymentBatch? PaymentBatch { get; set; }
    public SecondaryClient SecondaryClient { get; set; } = null!;
    public ServiceType ServiceType { get; set; } = null!;
    public ReconStatus ReconStatus { get; set; } = null!;
    public AppUser? CreatedByUser { get; set; }
    public ICollection<PaymentRecord> PaymentRecords { get; set; } = new List<PaymentRecord>();
    public ICollection<PaymentSummaryComment> Comments { get; set; } = new List<PaymentSummaryComment>();
}
