namespace PayOps.Database.Entities;

/// <summary>A primary client's own bank account (Nominated / Charges).</summary>
public class ClientBankAccount
{
    public int Id { get; set; }
    public int PrimaryClientId { get; set; }
    public byte AccountTypeId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? GlAccountNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public PrimaryClient PrimaryClient { get; set; } = null!;
    public AccountType AccountType { get; set; } = null!;
}
