namespace PayOps.Database.Entities;

/// <summary>Fee charged per payment record for a service type, from <see cref="EffectiveFrom"/> until the next rate.</summary>
public class BillingRate
{
    public int Id { get; set; }
    public byte ServiceTypeId { get; set; }
    public decimal FeePerRecord { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ServiceType ServiceType { get; set; } = null!;
}
