namespace PayOps.Database.Entities;

/// <summary>Lease that lets only one PayOps.Worker instance run a scheduled job at a time.</summary>
public class JobLease
{
    public string JobName { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? LastRunDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
