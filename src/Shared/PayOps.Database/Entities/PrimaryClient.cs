namespace PayOps.Database.Entities;

/// <summary>Top-level client. Owns one or more secondary clients; batches are never linked to a primary client directly.</summary>
public class PrimaryClient
{
    public int Id { get; set; }
    public string UniqueClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool AutoProcess { get; set; }
    public string? LogoFileName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public ICollection<SecondaryClient> SecondaryClients { get; set; } = new List<SecondaryClient>();
}
