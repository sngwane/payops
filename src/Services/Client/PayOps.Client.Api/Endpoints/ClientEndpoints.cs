using PayOps.Client.Api.Authentication;
using PayOps.Client.Data;
using PayOps.Client.Data.Services;

namespace PayOps.Client.Api.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(ClientApiAuth.ReadPolicy);
        MapPrimaryClients(api.MapGroup("/primary-clients").WithTags("Primary clients"));
        MapSecondaryClients(api.MapGroup("/secondary-clients").WithTags("Secondary clients"));
        MapBankAccounts(api);
        MapApiKeys(api);
        return app;
    }

    private static void MapPrimaryClients(RouteGroupBuilder group)
    {
        group.MapGet("/", async (PrimaryClientService service, string? search, bool? isActive, int? page, int? pageSize, CancellationToken ct)
            => (await service.ListAsync(new ClientListQuery(search, isActive, page ?? 1, pageSize ?? ClientListQuery.DefaultPageSize), ct)).ToHttp());
        group.MapGet("/{id:int}", async (int id, PrimaryClientService service, CancellationToken ct)
            => (await service.GetAsync(id, ct)).ToHttp());
        group.MapPost("/", async (CreatePrimaryClientRequest request, PrimaryClientService service, CancellationToken ct)
            => (await service.CreateAsync(request, ct)).ToCreated(c => $"/api/primary-clients/{c.Id}")).Write();
        group.MapPut("/{id:int}", async (int id, UpdatePrimaryClientRequest request, PrimaryClientService service, CancellationToken ct)
            => (await service.UpdateAsync(id, request, ct)).ToHttp()).Write();
        group.MapPost("/{id:int}/activate", async (int id, PrimaryClientService service, CancellationToken ct)
            => (await service.ActivateAsync(id, ct)).ToHttp()).Write();
        group.MapPost("/{id:int}/deactivate", async (int id, PrimaryClientService service, CancellationToken ct)
            => (await service.DeactivateAsync(id, ct)).ToHttp()).Write();

        group.MapGet("/{id:int}/secondary-clients", async (int id, SecondaryClientService service, string? search, bool? isActive, bool? requiresReview, int? page, int? pageSize, CancellationToken ct)
            => (await service.ListAsync(new SecondaryClientListQuery(search, isActive, requiresReview, id, page ?? 1, pageSize ?? ClientListQuery.DefaultPageSize), ct)).ToHttp());
        group.MapPost("/{id:int}/secondary-clients", async (int id, CreateSecondaryClientRequest request, SecondaryClientService service, CancellationToken ct)
            => (await service.CreateAsync(id, request, ct)).ToCreated(c => $"/api/secondary-clients/{c.Id}")).Write();
    }

    private static void MapSecondaryClients(RouteGroupBuilder group)
    {
        group.MapGet("/", async (SecondaryClientService service, string? search, bool? isActive, bool? requiresReview, int? page, int? pageSize, CancellationToken ct)
            => (await service.ListAsync(new SecondaryClientListQuery(search, isActive, requiresReview, null, page ?? 1, pageSize ?? ClientListQuery.DefaultPageSize), ct)).ToHttp());
        group.MapGet("/{id:int}", async (int id, SecondaryClientService service, CancellationToken ct)
            => (await service.GetAsync(id, ct)).ToHttp());
        group.MapGet("/by-unique-id/{uniqueClientId}", async (string uniqueClientId, SecondaryClientService service, CancellationToken ct)
            => (await service.GetByUniqueClientIdAsync(uniqueClientId, ct)).ToHttp());
        group.MapPut("/{id:int}", async (int id, UpdateSecondaryClientRequest request, SecondaryClientService service, CancellationToken ct)
            => (await service.UpdateAsync(id, request, ct)).ToHttp()).Write();
        group.MapPost("/{id:int}/activate", async (int id, SecondaryClientService service, CancellationToken ct)
            => (await service.ActivateAsync(id, ct)).ToHttp()).Write();
        group.MapPost("/{id:int}/deactivate", async (int id, SecondaryClientService service, CancellationToken ct)
            => (await service.DeactivateAsync(id, ct)).ToHttp()).Write();
        group.MapPost("/{id:int}/reassign", async (int id, ReassignSecondaryClientRequest request, SecondaryClientService service, CancellationToken ct)
            => (await service.ReassignAsync(id, request, ct)).ToHttp()).Write();
    }

    private static void MapBankAccounts(RouteGroupBuilder api)
    {
        var byClient = api.MapGroup("/primary-clients/{primaryClientId:int}/bank-accounts").WithTags("Bank accounts");
        byClient.MapGet("/", async (int primaryClientId, ClientBankAccountService service, CancellationToken ct)
            => (await service.ListAsync(primaryClientId, ct)).ToHttp());
        byClient.MapPost("/", async (int primaryClientId, CreateBankAccountRequest request, ClientBankAccountService service, CancellationToken ct)
            => (await service.CreateAsync(primaryClientId, request, ct)).ToCreated(a => $"/api/bank-accounts/{a.Id}")).Write();

        var accounts = api.MapGroup("/bank-accounts").WithTags("Bank accounts");
        accounts.MapGet("/{id:int}", async (int id, ClientBankAccountService service, CancellationToken ct)
            => (await service.GetAsync(id, ct)).ToHttp());
        accounts.MapPut("/{id:int}", async (int id, UpdateBankAccountRequest request, ClientBankAccountService service, CancellationToken ct)
            => (await service.UpdateAsync(id, request, ct)).ToHttp()).Write();
        accounts.MapPost("/{id:int}/deactivate", async (int id, ClientBankAccountService service, CancellationToken ct)
            => (await service.DeactivateAsync(id, ct)).ToHttp()).Write();
    }

    private static void MapApiKeys(RouteGroupBuilder api)
    {
        foreach (var (route, kind) in new[] { ("/primary-clients/{clientId:int}/api-keys", ClientKind.Primary), ("/secondary-clients/{clientId:int}/api-keys", ClientKind.Secondary) })
        {
            var group = api.MapGroup(route).WithTags("API keys");
            group.MapGet("/", async (int clientId, ClientApiKeyService service, CancellationToken ct)
                => (await service.ListAsync(kind, clientId, ct)).ToHttp());
            group.MapPost("/", async (int clientId, CreateApiKeyRequest request, ClientApiKeyService service, CancellationToken ct)
                => (await service.CreateAsync(kind, clientId, request, ct)).ToCreated(k => $"/api/api-keys/{k.ApiKey.Id}")).Write();
        }

        api.MapPost("/api-keys/{id:int}/revoke", async (int id, ClientApiKeyService service, CancellationToken ct)
            => (await service.RevokeAsync(id, ct)).ToHttp()).WithTags("API keys").Write();
    }

    private static RouteHandlerBuilder Write(this RouteHandlerBuilder builder) => builder.RequireAuthorization(ClientApiAuth.WritePolicy);
}
