namespace PayOps.Database.Entities;

/// <summary>Non-processing day. Batches may not be dated on a public holiday.</summary>
public class PublicHoliday
{
    public int Id { get; set; }
    public DateOnly HolidayDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
