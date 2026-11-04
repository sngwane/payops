namespace PayOps.Database.Entities;

/// <summary>Application user. Authentication is done by Entra ID; this table holds identity mapping and authorization only.</summary>
public class AppUser
{
    public int Id { get; set; }
    public Guid ExternalId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
