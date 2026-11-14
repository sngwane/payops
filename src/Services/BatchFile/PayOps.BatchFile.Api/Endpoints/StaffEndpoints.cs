using PayOps.BatchFile.Api.Authentication;
using PayOps.BatchFile.Data;
using PayOps.BatchFile.Data.Services;
using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Api.Endpoints;

/// <summary>Endpoints for PayOps staff (Entra ID): the upload log, public holidays and billing rates.</summary>
public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(BatchFileAuth.StaffReadPolicy);

        api.MapGet("/uploads", async (BatchQueries queries, string? uniqueClientId, bool? isAccepted, DateTime? from, DateTime? to, int? page, int? pageSize,
                CancellationToken ct)
            => (await queries.ListUploadsAsync(new UploadListQuery(uniqueClientId, isAccepted, from, to, page ?? 1, pageSize ?? BatchListQuery.DefaultPageSize), ct))
                .ToHttp()).WithTags("Uploads");

        var holidays = api.MapGroup("/public-holidays").WithTags("Public holidays");
        holidays.MapGet("/", async (ReferenceDataService service, int? year, CancellationToken ct) => TypedResults.Ok(await service.ListHolidaysAsync(year, ct)));
        holidays.MapPost("/", async (PublicHolidayRequest request, ReferenceDataService service, CancellationToken ct)
            => (await service.CreateHolidayAsync(request, ct)).ToCreated(h => $"/api/public-holidays/{h.Id}")).Write();
        holidays.MapPut("/{id:int}", async (int id, PublicHolidayRequest request, ReferenceDataService service, CancellationToken ct)
            => (await service.UpdateHolidayAsync(id, request, ct)).ToHttp()).Write();

        var rates = api.MapGroup("/billing-rates").WithTags("Billing rates");
        rates.MapGet("/", async (ReferenceDataService service, ServiceTypeCode? serviceType, CancellationToken ct)
            => TypedResults.Ok(await service.ListBillingRatesAsync(serviceType, ct)));
        rates.MapPost("/", async (CreateBillingRateRequest request, ReferenceDataService service, CancellationToken ct)
            => (await service.CreateBillingRateAsync(request, ct)).ToCreated(r => $"/api/billing-rates/{r.Id}")).Write();
        rates.MapPut("/{id:int}", async (int id, UpdateBillingRateRequest request, ReferenceDataService service, CancellationToken ct)
            => (await service.UpdateBillingRateAsync(id, request, ct)).ToHttp()).Write();

        return app;
    }

    private static RouteHandlerBuilder Write(this RouteHandlerBuilder builder) => builder.RequireAuthorization(BatchFileAuth.StaffWritePolicy);
}
