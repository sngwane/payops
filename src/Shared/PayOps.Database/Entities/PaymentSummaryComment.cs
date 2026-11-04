namespace PayOps.Database.Entities;

public class PaymentSummaryComment
{
    public int Id { get; set; }
    public int PaymentSummaryId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }

    public PaymentSummary PaymentSummary { get; set; } = null!;
    public AppUser? CreatedByUser { get; set; }
}
