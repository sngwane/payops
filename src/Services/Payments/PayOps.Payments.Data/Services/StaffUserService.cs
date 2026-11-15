using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;

namespace PayOps.Payments.Data.Services;

/// <summary>Maps the signed-in staff member to an <see cref="AppUser"/> row, creating it on their first change.</summary>
public sealed class StaffUserService(PaymentsDbContext db, TimeProvider clock)
{
    private readonly Dictionary<Guid, int> _ids = [];

    public async Task<int> GetUserIdAsync(StaffIdentity who, CancellationToken ct = default)
    {
        if (_ids.TryGetValue(who.ExternalId, out var cached))
            return cached;
        var id = await FindAsync(who.ExternalId, ct) ?? await CreateAsync(who, ct);
        _ids[who.ExternalId] = id;
        return id;
    }

    private Task<int?> FindAsync(Guid externalId, CancellationToken ct)
        => db.AppUsers.AsNoTracking().Where(u => u.ExternalId == externalId).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);

    private async Task<int> CreateAsync(StaffIdentity who, CancellationToken ct)
    {
        var fallback = $"{who.ExternalId:N}";
        var username = Clip(who.Username, 100) ?? fallback;
        if (await db.AppUsers.AnyAsync(u => u.Username == username, ct))
            username = fallback;
        var email = Clip(who.Email, 255) ?? $"{fallback}@users.payops.local";
        if (await db.AppUsers.AnyAsync(u => u.Email == email, ct))
            email = $"{fallback}@users.payops.local";

        var now = clock.GetUtcNow().UtcDateTime;
        var user = new AppUser
        {
            ExternalId = who.ExternalId,
            Username = username,
            Email = email,
            FirstName = Clip(who.FirstName, 100) ?? username,
            LastName = Clip(who.LastName, 100) ?? string.Empty,
            CreatedDate = now,
            LastLoginDate = now
        };
        db.AppUsers.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
            return user.Id;
        }
        catch (DbUpdateException)
        {
            // Another request created the same user at the same time.
            db.Entry(user).State = EntityState.Detached;
            return await FindAsync(who.ExternalId, ct) ?? throw new InvalidOperationException($"Could not create the PayOps user for {who.ExternalId}.");
        }
    }

    private static string? Clip(string? value, int max)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];
    }
}
