using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PayOps.Payments.Data;

namespace PayOps.Payments.Api.Authentication;

/// <summary>
/// The signed-in staff member. Entra tokens carry the user's object id ("oid"), which becomes AppUser.ExternalId.
/// Outside Production-like environments a caller without an oid (the Development sign-in) is the fixed "dev.user".
/// </summary>
public sealed class CurrentStaff(IHttpContextAccessor accessor, IHostEnvironment environment)
{
    public const string DevelopmentUsername = "dev.user";

    public StaffIdentity? Identity => Resolve(accessor.HttpContext?.User);

    private StaffIdentity? Resolve(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;
        var name = user.FindFirstValue("name") ?? user.Identity.Name;
        var username = user.FindFirstValue("preferred_username") ?? user.FindFirstValue("upn") ?? name;
        var objectId = user.FindFirstValue("oid") ?? user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        if (Guid.TryParse(objectId, out var externalId))
            return new StaffIdentity(externalId, username ?? externalId.ToString(), user.FindFirstValue("email") ?? user.FindFirstValue("preferred_username"),
                user.FindFirstValue("given_name"), user.FindFirstValue("family_name"));

        if (environment.IsProduction() || environment.IsStaging())
            return null;
        var devName = username is null or "developer" ? DevelopmentUsername : username;
        return new StaffIdentity(new Guid(MD5.HashData(Encoding.UTF8.GetBytes("payops-dev:" + devName))), devName, null, devName, "(development)");
    }
}
