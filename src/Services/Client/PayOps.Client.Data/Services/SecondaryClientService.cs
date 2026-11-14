using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;

namespace PayOps.Client.Data.Services;

public class SecondaryClientService(ClientDbContext db, TimeProvider clock)
{
    public async Task<ClientResult<PagedResult<SecondaryClientDto>>> ListAsync(SecondaryClientListQuery query, CancellationToken cancellationToken = default)
    {
        if (Validation.Paging(query.Page, query.PageSize) is { } error)
            return error;
        if (query.PrimaryClientId is { } primaryId && !await db.PrimaryClients.AnyAsync(p => p.Id == primaryId, cancellationToken))
            return ClientError.NotFound($"Primary client {primaryId} was not found.");

        var clients = db.SecondaryClients.AsNoTracking();
        if (query.PrimaryClientId is { } primaryClientId)
            clients = clients.Where(s => s.PrimaryClientId == primaryClientId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            clients = clients.Where(s => s.Name.Contains(search) || s.UniqueClientId.StartsWith(search));
        }
        if (query.IsActive is { } isActive)
            clients = clients.Where(s => s.IsActive == isActive);
        if (query.RequiresReview is { } requiresReview)
            clients = clients.Where(s => s.RequiresReview == requiresReview);

        var total = await clients.CountAsync(cancellationToken);
        var items = await Project(clients.OrderBy(s => s.Name).ThenBy(s => s.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<SecondaryClientDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<ClientResult<SecondaryClientDto>> GetAsync(int id, CancellationToken cancellationToken = default)
        => await Project(db.SecondaryClients.AsNoTracking().Where(s => s.Id == id)).SingleOrDefaultAsync(cancellationToken) is { } dto
            ? dto
            : NotFound(id);

    public async Task<ClientResult<SecondaryClientDto>> GetByUniqueClientIdAsync(string uniqueClientId, CancellationToken cancellationToken = default)
        => await Project(db.SecondaryClients.AsNoTracking().Where(s => s.UniqueClientId == uniqueClientId)).SingleOrDefaultAsync(cancellationToken) is { } dto
            ? dto
            : ClientError.NotFound($"Secondary client '{uniqueClientId}' was not found.");

    /// <summary>Creates a secondary client under an active primary client.</summary>
    public async Task<ClientResult<SecondaryClientDto>> CreateAsync(int primaryClientId, CreateSecondaryClientRequest request, CancellationToken cancellationToken = default)
    {
        var v = new Validation();
        var uniqueClientId = v.UniqueClientId("uniqueClientId", request.UniqueClientId);
        var name = v.Required("name", request.Name, Validation.NameMaxLength);
        if (!v.IsValid)
            return v.ToError();

        var primary = await db.PrimaryClients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == primaryClientId, cancellationToken);
        if (primary is null)
            return ClientError.NotFound($"Primary client {primaryClientId} was not found.");
        if (!primary.IsActive)
            return ClientError.Conflict("Secondary clients can't be added to an inactive primary client.");
        if (await ClientIdentifiers.IsTakenAsync(db, uniqueClientId, cancellationToken))
            return ClientIdentifiers.Taken(uniqueClientId);

        var client = new SecondaryClient { PrimaryClientId = primaryClientId, UniqueClientId = uniqueClientId, Name = name };
        db.SecondaryClients.Add(client);
        if (await DbErrors.SaveAsync(db, cancellationToken, ClientIdentifiers.Taken(uniqueClientId).Message) is { } saveError)
            return saveError;
        return await GetAsync(client.Id, cancellationToken);
    }

    public async Task<ClientResult<SecondaryClientDto>> UpdateAsync(int id, UpdateSecondaryClientRequest request, CancellationToken cancellationToken = default)
    {
        var v = new Validation();
        var name = v.Required("name", request.Name, Validation.NameMaxLength);
        var rowVersion = v.RowVersion("rowVersion", request.RowVersion);
        if (!v.IsValid)
            return v.ToError();

        var client = await db.SecondaryClients.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (client is null)
            return NotFound(id);

        db.Entry(client).Property(s => s.RowVersion).OriginalValue = rowVersion;
        client.Name = name;
        client.ModifiedDate = clock.GetUtcNow().UtcDateTime;
        if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
            return saveError;
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Moves a placeholder (RequiresReview) secondary client to a real, active primary client and clears the flag.</summary>
    public async Task<ClientResult<SecondaryClientDto>> ReassignAsync(int id, ReassignSecondaryClientRequest request, CancellationToken cancellationToken = default)
    {
        var v = new Validation();
        var rowVersion = v.RowVersion("rowVersion", request.RowVersion);
        if (!v.IsValid)
            return v.ToError();

        var client = await db.SecondaryClients.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (client is null)
            return NotFound(id);
        if (!client.RequiresReview)
            return ClientError.Conflict("Only secondary clients flagged for review can be moved to another primary client.");

        var target = await db.PrimaryClients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.PrimaryClientId, cancellationToken);
        if (target is null)
            return ClientError.NotFound($"Primary client {request.PrimaryClientId} was not found.");
        if (!target.IsActive)
            return ClientError.Conflict("Secondary clients can't be moved to an inactive primary client.");
        if (target.Id == client.PrimaryClientId)
            return ClientError.Conflict("The secondary client already belongs to this primary client.");

        db.Entry(client).Property(s => s.RowVersion).OriginalValue = rowVersion;
        client.PrimaryClientId = target.Id;
        client.RequiresReview = false;
        client.ModifiedDate = clock.GetUtcNow().UtcDateTime;
        if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
            return saveError;
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Only allowed while the primary client is active.</summary>
    public async Task<ClientResult<SecondaryClientDto>> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await db.SecondaryClients.Include(s => s.PrimaryClient).SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (client is null)
            return NotFound(id);
        if (!client.PrimaryClient.IsActive)
            return ClientError.Conflict("Activate the primary client first.");
        return await SetActiveAsync(client, true, cancellationToken);
    }

    /// <summary>Existing batches are untouched; new uploads from an inactive client are refused by the Batch File API.</summary>
    public async Task<ClientResult<SecondaryClientDto>> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await db.SecondaryClients.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (client is null)
            return NotFound(id);
        return await SetActiveAsync(client, false, cancellationToken);
    }

    private async Task<ClientResult<SecondaryClientDto>> SetActiveAsync(SecondaryClient client, bool isActive, CancellationToken cancellationToken)
    {
        if (client.IsActive != isActive)
        {
            client.IsActive = isActive;
            client.ModifiedDate = clock.GetUtcNow().UtcDateTime;
            if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
                return saveError;
        }
        return await GetAsync(client.Id, cancellationToken);
    }

    private static ClientError NotFound(int id) => ClientError.NotFound($"Secondary client {id} was not found.");

    private static IQueryable<SecondaryClientDto> Project(IQueryable<SecondaryClient> clients)
        => clients.Select(s => new SecondaryClientDto(
            s.Id, s.PrimaryClientId, s.PrimaryClient.UniqueClientId, s.PrimaryClient.Name, s.UniqueClientId, s.Name,
            s.IsActive, s.RequiresReview, s.CreatedDate, s.ModifiedDate, DbErrors.ToRowVersion(s.RowVersion)));
}
