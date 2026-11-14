namespace PayOps.ServiceDefaults;

/// <summary>Entra ID app roles used by the PayOps APIs.</summary>
public static class PayOpsRoles
{
    public const string ClientAdmin = "PayOps.ClientAdmin";
    public const string Reader = "PayOps.Reader";
    public const string Operator = "PayOps.Operator";
    public const string Approver = "PayOps.Approver";

    public static readonly string[] All = [ClientAdmin, Reader, Operator, Approver];
}
