namespace PayOps.Database.Entities;

public class UserRole
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedDate { get; set; }

    public AppUser AppUser { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
