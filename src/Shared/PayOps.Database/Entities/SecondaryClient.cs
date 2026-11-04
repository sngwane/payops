namespace PayOps.Database.Entities;

/// <summary>Client that submits payment batches. Always belongs to a primary client.</summary>
public class SecondaryClient
{
    public int Id { get; set; }
    public int PrimaryClientId { get; set; }
    public string UniqueClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public PrimaryClient PrimaryClient { get; set; } = null!;
    public ICollection<PaymentSummary> PaymentSummaries { get; set; } = new List<PaymentSummary>();
}
