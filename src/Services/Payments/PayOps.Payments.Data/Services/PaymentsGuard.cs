using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Payments.Data.Services;

internal static class PaymentsGuard
{
    public static PaymentsError? CheckPaging(Paging paging)
    {
        var errors = new Dictionary<string, string[]>();
        if (paging.Page < 1)
            errors["page"] = ["page must be 1 or more."];
        if (paging.PageSize is < 1 or > Paging.MaxPageSize)
            errors["pageSize"] = [$"pageSize must be between 1 and {Paging.MaxPageSize}."];
        return errors.Count == 0 ? null : PaymentsError.Invalid(errors);
    }

    public static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> query, Paging paging, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, paging.Page, paging.PageSize, total);
    }

    /// <summary>Required free text, trimmed; null when valid, otherwise adds the error.</summary>
    public static string? Text(Dictionary<string, string[]> errors, string field, string? value, int maxLength, bool required = true)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            if (required)
                errors[field] = [$"{field} is required."];
            return null;
        }
        if (text.Length > maxLength)
            errors[field] = [$"{field} must be at most {maxLength} characters."];
        return text;
    }

    /// <summary>Applies the caller's RowVersion (base64) so a stale edit fails with 409 instead of overwriting.</summary>
    public static void ApplyRowVersion<TEntity>(PaymentsDbContext db, TEntity entity, string? rowVersion, Dictionary<string, string[]> errors)
        where TEntity : class
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            return;
        try
        {
            db.Entry(entity).Property("RowVersion").OriginalValue = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            errors["rowVersion"] = ["rowVersion is not valid base64."];
        }
    }

    public static async Task<PaymentsError?> SaveAsync(PaymentsDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return PaymentsError.Conflict("The record was changed by someone else. Reload it and try again.");
        }
    }

    /// <summary>Queues an outbox message for the Notifications API unless one with the same key already exists.</summary>
    public static async Task QueueNotificationAsync(PaymentsDbContext db, NotificationType type, string key, PaymentSummary summary, object payload, CancellationToken ct)
    {
        var deduplicationKey = $"{type}:{key}";
        if (await db.NotificationMessages.AnyAsync(m => m.DeduplicationKey == deduplicationKey, ct))
            return;
        db.NotificationMessages.Add(new NotificationMessage
        {
            Type = type,
            DeduplicationKey = deduplicationKey,
            SecondaryClientId = summary.SecondaryClientId,
            PaymentSummaryId = summary.Id,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload)
        });
    }
}
