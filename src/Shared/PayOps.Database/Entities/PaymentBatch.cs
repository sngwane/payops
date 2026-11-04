namespace PayOps.Database.Entities;

/// <summary>One bank run / bank file. Requires maker-checker approval before it is sent.</summary>
public class PaymentBatch
{
    public int Id { get; set; }
    public int BankId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime RunDate { get; set; }
    public int TotalRecords { get; set; }
    public decimal TotalValue { get; set; }
    public byte BatchStatusId { get; set; }
    public byte ApprovalStatusId { get; set; }
    public string? FileName { get; set; }
    public string? Comment { get; set; }
    public int CreatedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ProcessedDate { get; set; }

    public Bank Bank { get; set; } = null!;
    public BatchStatus BatchStatus { get; set; } = null!;
    public ApprovalStatus ApprovalStatus { get; set; } = null!;
    public AppUser CreatedByUser { get; set; } = null!;
    public AppUser? ApprovedByUser { get; set; }
    public ICollection<PaymentSummary> PaymentSummaries { get; set; } = new List<PaymentSummary>();
}
