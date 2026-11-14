using PayOps.Database.Lookups;

namespace PayOps.Database.Entities;

/// <summary>Contact supplied with a batch upload; overrides the client's contacts for that batch.</summary>
public class BatchContact
{
    public long Id { get; set; }
    public int PaymentSummaryId { get; set; }
    public ContactPurpose Purpose { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CellNumber { get; set; }

    public PaymentSummary PaymentSummary { get; set; } = null!;
}
