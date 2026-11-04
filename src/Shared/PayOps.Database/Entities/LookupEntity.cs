namespace PayOps.Database.Entities;

public abstract class LookupEntity
{
    public byte Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
