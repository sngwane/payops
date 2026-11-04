namespace PayOps.Database.Entities;

/// <summary>One employee/beneficiary payment within a client batch.</summary>
public class PaymentRecord
{
    public long Id { get; set; }
    public int PaymentSummaryId { get; set; }
    public string? BankFileReference { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? Initials { get; set; }
    public string? IdOrPassport { get; set; }
    public string? Cell { get; set; }
    public string? Email { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Employee account type as supplied in the upload file (free text, max 10).</summary>
    public string AccountType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? BankReference { get; set; }
    public byte TransactionStatusId { get; set; }
    public string? RejectionReason { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public PaymentSummary PaymentSummary { get; set; } = null!;
    public TransactionStatus TransactionStatus { get; set; } = null!;
    public PaymentRejection? PaymentRejection { get; set; }
    public ICollection<PaymentRecordStatusHistory> StatusHistory { get; set; } = new List<PaymentRecordStatusHistory>();
}
