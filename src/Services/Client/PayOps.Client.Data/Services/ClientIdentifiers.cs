using Microsoft.EntityFrameworkCore;

namespace PayOps.Client.Data.Services;

internal static class ClientIdentifiers
{
    /// <summary>UniqueClientId must be unique across primary and secondary clients so lookups are never ambiguous.</summary>
    public static async Task<bool> IsTakenAsync(ClientDbContext db, string uniqueClientId, CancellationToken cancellationToken)
        => await db.PrimaryClients.AnyAsync(p => p.UniqueClientId == uniqueClientId, cancellationToken)
           || await db.SecondaryClients.AnyAsync(s => s.UniqueClientId == uniqueClientId, cancellationToken);

    public static ClientError Taken(string uniqueClientId) => ClientError.Conflict($"UniqueClientId '{uniqueClientId}' is already used by another client.");
}
