namespace PayOps.Database.Entities;

/// <summary>Access key of a primary or secondary client (exactly one owner). Only the SHA-256 hash of the key is stored.</summary>
public class ClientApiKey
{
    public int Id { get; set; }
    public int? PrimaryClientId { get; set; }
    public int? SecondaryClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public byte[] KeyHash { get; set; } = [];
    public DateTime? ExpiryDate { get; set; }
    public DateTime? RevokedDate { get; set; }
    public DateTime? LastUsedDate { get; set; }
    public DateTime CreatedDate { get; set; }

    public PrimaryClient? PrimaryClient { get; set; }
    public SecondaryClient? SecondaryClient { get; set; }
}
