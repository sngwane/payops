using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;

namespace PayOps.Client.Data.Services;

public class PrimaryClientService(ClientDbContext db, TimeProvider clock)
{
    public async Task<ClientResult<PagedResult<PrimaryClientDto>>> ListAsync(ClientListQuery query, CancellationToken cancellationToken = default)
    {
        if (Validation.Paging(query.Page, query.PageSize) is { } error)
            return error;

        var clients = db.PrimaryClients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            clients = clients.Where(p => p.Name.Contains(search) || p.UniqueClientId.StartsWith(search));
        }
        if (query.IsActive is { } isActive)
            clients = clients.Where(p => p.IsActive == isActive);

        var total = await clients.CountAsync(cancellationToken);
        var items = await Project(clients.OrderBy(p => p.Name).ThenBy(p => p.Id)
                .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<PrimaryClientDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<ClientResult<PrimaryClientDto>> GetAsync(int id, CancellationToken cancellationToken = default)
        => await Project(db.PrimaryClients.AsNoTracking().Where(p => p.Id == id)).SingleOrDefaultAsync(cancellationToken) is { } dto
            ? dto
            : NotFound(id);

    public async Task<ClientResult<PrimaryClientDto>> CreateAsync(CreatePrimaryClientRequest request, CancellationToken cancellationToken = default)
    {
        var v = new Validation();
        var uniqueClientId = v.UniqueClientId("uniqueClientId", request.UniqueClientId);
        var name = v.Required("name", request.Name, Validation.NameMaxLength);
        var logo = v.Optional("logoFileName", request.LogoFileName, Validation.LogoFileNameMaxLength);
        if (!v.IsValid)
            return v.ToError();
        if (await ClientIdentifiers.IsTakenAsync(db, uniqueClientId, cancellationToken))
            return ClientIdentifiers.Taken(uniqueClientId);

        var client = new PrimaryClient { UniqueClientId = uniqueClientId, Name = name, AutoProcess = request.AutoProcess, LogoFileName = logo };
        db.PrimaryClients.Add(client);
        if (await DbErrors.SaveAsync(db, cancellationToken, ClientIdentifiers.Taken(uniqueClientId).Message) is { } saveError)
            return saveError;
        return await GetAsync(client.Id, cancellationToken);
    }

    public async Task<ClientResult<PrimaryClientDto>> UpdateAsync(int id, UpdatePrimaryClientRequest request, CancellationToken cancellationToken = default)
    {
        var v = new Validation();
        var name = v.Required("name", request.Name, Validation.NameMaxLength);
        var logo = v.Optional("logoFileName", request.LogoFileName, Validation.LogoFileNameMaxLength);
        var rowVersion = v.RowVersion("rowVersion", request.RowVersion);
        if (!v.IsValid)
            return v.ToError();

        var client = await db.PrimaryClients.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (client is null)
            return NotFound(id);

        db.Entry(client).Property(p => p.RowVersion).OriginalValue = rowVersion;
        client.Name = name;
        client.AutoProcess = request.AutoProcess;
        client.LogoFileName = logo;
        client.ModifiedDate = clock.GetUtcNow().UtcDateTime;
        if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
            return saveError;
        return await GetAsync(id, cancellationToken);
    }

    public Task<ClientResult<PrimaryClientDto>> ActivateAsync(int id, CancellationToken cancellationToken = default)
        => SetActiveAsync(id, true, cancellationToken);

    /// <summary>Blocked while the primary client still has active secondary clients.</summary>
    public Task<ClientResult<PrimaryClientDto>> DeactivateAsync(int id, CancellationToken cancellationToken = default)
        => SetActiveAsync(id, false, cancellationToken);

    private async Task<ClientResult<PrimaryClientDto>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var client = await db.PrimaryClients.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (client is null)
            return NotFound(id);
        if (!isActive && await db.SecondaryClients.AnyAsync(s => s.PrimaryClientId == id && s.IsActive, cancellationToken))
            return ClientError.Conflict("Deactivate the primary client's active secondary clients first.");

        if (client.IsActive != isActive)
        {
            client.IsActive = isActive;
            client.ModifiedDate = clock.GetUtcNow().UtcDateTime;
            if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
                return saveError;
        }
        return await GetAsync(id, cancellationToken);
    }

    private static ClientError NotFound(int id) => ClientError.NotFound($"Primary client {id} was not found.");

    private static IQueryable<PrimaryClientDto> Project(IQueryable<PrimaryClient> clients)
        => clients.Select(p => new PrimaryClientDto(
            p.Id, p.UniqueClientId, p.Name, p.AutoProcess, p.LogoFileName, p.IsActive, p.SecondaryClients.Count(),
            p.CreatedDate, p.ModifiedDate, DbErrors.ToRowVersion(p.RowVersion)));
}
