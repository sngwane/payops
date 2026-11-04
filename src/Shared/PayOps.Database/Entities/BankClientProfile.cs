namespace PayOps.Database.Entities;

/// <summary>Bank file header settings and file sequence counters per bank profile.</summary>
public class BankClientProfile
{
    public int Id { get; set; }
    public int BankId { get; set; }
    public string ClientProfileNumber { get; set; } = string.Empty;
    public string ClientIdentifier { get; set; } = string.Empty;
    public long RunningSequence { get; set; }
    public int DailySequence { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public bool IsActive { get; set; } = true;

    public Bank Bank { get; set; } = null!;
}
