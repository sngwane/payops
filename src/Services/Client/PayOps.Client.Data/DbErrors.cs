using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PayOps.Client.Data;

internal static class DbErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };

    public static async Task<ClientError?> SaveAsync(DbContext db, CancellationToken cancellationToken, string duplicateMessage = "A record with the same unique value already exists.")
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ClientError.Conflict("The record was changed by someone else. Reload it and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return ClientError.Conflict(duplicateMessage);
        }
    }

    public static string ToRowVersion(byte[] rowVersion) => Convert.ToBase64String(rowVersion);
}
