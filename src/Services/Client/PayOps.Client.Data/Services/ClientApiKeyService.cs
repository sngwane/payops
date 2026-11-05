using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;

namespace PayOps.Client.Data.Services;

/// <summary>API keys of primary and secondary clients. The full key is returned once, on creation; only its hash is stored.</summary>
public class ClientApiKeyService(ClientDbContext db, TimeProvider clock)
{
    /// <summary>Two active keys per client let a key be rotated without downtime.</summary>
    public const int MaxActiveKeysPerClient = 2;

    public async Task<ClientResult<IReadOnlyList<ClientApiKeyDto>>> ListAsync(ClientKind kind, int clientId, CancellationToken cancellationToken = default)
    {
        if (await FindOwnerErrorAsync(kind, clientId, cancellationToken) is { } error)
            return error;
        var items = await Project(OwnedBy(kind, clientId).AsNoTracking().OrderByDescending(k => k.CreatedDate).ThenByDescending(k => k.Id))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<ClientResult<CreatedClientApiKeyDto>> CreateAsync(ClientKind kind, int clientId, CreateApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var v = new Validation();
        var name = v.Required("name", request.Name, Validation.NameMaxLength);
        if (request.ExpiryDate is { } expiry && expiry.ToUniversalTime() <= now)
            v.Add("expiryDate", "Must be in the future.");
        if (!v.IsValid)
            return v.ToError();

        if (await FindOwnerErrorAsync(kind, clientId, cancellationToken) is { } error)
            return error;
        var activeKeys = await OwnedBy(kind, clientId)
            .CountAsync(k => k.RevokedDate == null && (k.ExpiryDate == null || k.ExpiryDate > now), cancellationToken);
        if (activeKeys >= MaxActiveKeysPerClient)
            return ClientError.Conflict($"The client already has {MaxActiveKeysPerClient} active API keys. Revoke one first.");

        var key = ApiKeyHasher.Generate();
        var apiKey = new ClientApiKey
        {
            PrimaryClientId = kind == ClientKind.Primary ? clientId : null,
            SecondaryClientId = kind == ClientKind.Secondary ? clientId : null,
            Name = name,
            KeyPrefix = ApiKeyHasher.Prefix(key),
            KeyHash = ApiKeyHasher.Hash(key),
            ExpiryDate = request.ExpiryDate?.ToUniversalTime()
        };
        db.ClientApiKeys.Add(apiKey);
        if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
            return saveError;

        var dto = await Project(db.ClientApiKeys.AsNoTracking().Where(k => k.Id == apiKey.Id)).SingleAsync(cancellationToken);
        return new CreatedClientApiKeyDto(dto, key);
    }

    /// <summary>Revoked keys can't be restored.</summary>
    public async Task<ClientResult<ClientApiKeyDto>> RevokeAsync(int id, CancellationToken cancellationToken = default)
    {
        var apiKey = await db.ClientApiKeys.SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
        if (apiKey is null)
            return ClientError.NotFound($"API key {id} was not found.");
        if (apiKey.RevokedDate is null)
        {
            apiKey.RevokedDate = clock.GetUtcNow().UtcDateTime;
            if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
                return saveError;
        }
        return await Project(db.ClientApiKeys.AsNoTracking().Where(k => k.Id == id)).SingleAsync(cancellationToken);
    }

    /// <summary>
    /// Resolves the client that owns <paramref name="key"/>. Returns null for unknown, revoked or expired keys and for keys of
    /// inactive clients (a secondary client also needs an active primary). For reuse by the Batch File API and CoreWCF facade.
    /// </summary>
    public async Task<ApiKeyOwner?> ValidateApiKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        var hash = ApiKeyHasher.Hash(key);
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.ClientApiKeys.AsNoTracking()
            .Where(k => k.KeyHash == hash && k.RevokedDate == null && (k.ExpiryDate == null || k.ExpiryDate > now))
            .Where(k => (k.PrimaryClient != null && k.PrimaryClient.IsActive)
                        || (k.SecondaryClient != null && k.SecondaryClient.IsActive && k.SecondaryClient.PrimaryClient.IsActive))
            .Select(k => k.PrimaryClient != null
                ? new ApiKeyOwner(ClientKind.Primary, k.PrimaryClient.Id, k.PrimaryClient.UniqueClientId, k.Id)
                : new ApiKeyOwner(ClientKind.Secondary, k.SecondaryClient!.Id, k.SecondaryClient.UniqueClientId, k.Id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<ClientApiKey> OwnedBy(ClientKind kind, int clientId)
        => kind == ClientKind.Primary
            ? db.ClientApiKeys.Where(k => k.PrimaryClientId == clientId)
            : db.ClientApiKeys.Where(k => k.SecondaryClientId == clientId);

    private async Task<ClientError?> FindOwnerErrorAsync(ClientKind kind, int clientId, CancellationToken cancellationToken)
    {
        var exists = kind == ClientKind.Primary
            ? await db.PrimaryClients.AnyAsync(p => p.Id == clientId, cancellationToken)
            : await db.SecondaryClients.AnyAsync(s => s.Id == clientId, cancellationToken);
        return exists ? null : ClientError.NotFound($"{kind} client {clientId} was not found.");
    }

    private static IQueryable<ClientApiKeyDto> Project(IQueryable<ClientApiKey> keys)
        => keys.Select(k => new ClientApiKeyDto(
            k.Id, k.PrimaryClientId, k.SecondaryClientId, k.Name, k.KeyPrefix, k.ExpiryDate, k.RevokedDate, k.LastUsedDate, k.CreatedDate));
}
