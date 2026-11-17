using Microsoft.AspNetCore.Mvc;
using PayOps.Database.Lookups;
using PayOps.Payments.Api.Authentication;
using PayOps.Payments.Data;
using PayOps.Payments.Data.Services;

namespace PayOps.Payments.Api.Endpoints;

public sealed record PageParameters(int? Page, int? PageSize, string? Format)
{
    public Paging Paging => new(Page ?? 1, PageSize ?? Paging.DefaultPageSize);
}

public sealed record BatchSearchParameters(
    string? Client, int? PrimaryClientId, int? SecondaryClientId, string? ClientReference, string? UniqueReference,
    ServiceTypeCode? ServiceType, DateOnly? PaymentDateFrom, DateOnly? PaymentDateTo, ReconStatusCode? ReconStatus,
    bool? OnHold, bool? InBankRun, bool? HasRejections)
{
    public OpsBatchQuery Query => new(Client, PrimaryClientId, SecondaryClientId, ClientReference, UniqueReference, ServiceType,
        PaymentDateFrom, PaymentDateTo, ReconStatus, OnHold, InBankRun, HasRejections);
}

public static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(PaymentsApiAuth.ReadPolicy);
        MapBatches(api.MapGroup("/batches").WithTags("Batches"));
        MapReconciliation(api);
        MapPayments(api);
        MapRejections(api.MapGroup("/rejections").WithTags("Rejections"));
        return app;
    }

    private static RouteHandlerBuilder Write(this RouteHandlerBuilder builder) => builder.RequireAuthorization(PaymentsApiAuth.WritePolicy);

    private static void MapBatches(RouteGroupBuilder group)
    {
        group.MapGet("/", async ([AsParameters] BatchSearchParameters search, [AsParameters] PageParameters page, HttpRequest request, OpsBatchQueries queries, CancellationToken ct)
            => CsvResult<OpsBatchDto>.Wanted(request, page.Format)
                ? queries.Export(search.Query).ToCsv("batches.csv")
                : (await queries.ListAsync(search.Query, page.Paging, ct)).ToHttp())
            .WithSummary("Search batches. Add ?format=csv (or Accept: text/csv) for a CSV of every match.");

        group.MapGet("/{id:int}", async (int id, OpsBatchQueries queries, CancellationToken ct) => (await queries.GetAsync(id, ct)).ToHttp());

        group.MapGet("/{id:int}/payments", async (int id, TransactionStatusCode? status, string? search, [AsParameters] PageParameters page, HttpRequest request, OpsBatchQueries queries, CancellationToken ct)
            => CsvResult<OpsPaymentDto>.Wanted(request, page.Format)
                ? (await queries.ExportPaymentsAsync(id, new OpsPaymentQuery(status, search), ct)).ToCsv($"batch-{id}-payments.csv")
                : (await queries.PaymentsAsync(id, new OpsPaymentQuery(status, search), page.Paging, ct)).ToHttp());

        group.MapGet("/{id:int}/comments", async (int id, OpsBatchQueries queries, CancellationToken ct) => (await queries.CommentsAsync(id, ct)).ToHttp());
        group.MapPost("/{id:int}/comments", async (int id, AddCommentRequest body, CurrentStaff staff, BatchOperationsService service, CancellationToken ct)
            => staff.Identity is not { } who ? ResultMapping.NoStaffIdentity() : (await service.AddCommentAsync(id, body, who, ct)).ToHttp()).Write();

        group.MapGet("/{id:int}/history", async (int id, OpsBatchQueries queries, CancellationToken ct) => (await queries.HistoryAsync(id, ct)).ToHttp())
            .WithSummary("Who changed what: reconciliation, hold and bank-run changes from the temporal history, plus comments.");

        group.MapPost("/{id:int}/reconciliation", async (int id, SetReconStatusRequest body, CurrentStaff staff, BatchOperationsService service, CancellationToken ct)
            => staff.Identity is not { } who ? ResultMapping.NoStaffIdentity() : (await service.SetReconStatusAsync(id, body, who, ct)).ToHttp()).Write()
            .WithSummary("Set Reconciled / Unreconciled / Discrepancies with a comment. Locked once the batch is in a bank run.");
        group.MapPost("/{id:int}/hold", async (int id, HoldRequest body, CurrentStaff staff, BatchOperationsService service, CancellationToken ct)
            => staff.Identity is not { } who ? ResultMapping.NoStaffIdentity() : (await service.HoldAsync(id, body, who, ct)).ToHttp()).Write()
            .WithSummary("Put the whole batch on hold (only before it is in a bank run).");
        group.MapPost("/{id:int}/release", async (int id, ReleaseRequest body, CurrentStaff staff, BatchOperationsService service, CancellationToken ct)
            => staff.Identity is not { } who ? ResultMapping.NoStaffIdentity() : (await service.ReleaseAsync(id, body, who, ct)).ToHttp()).Write();
    }

    private static void MapReconciliation(RouteGroupBuilder api)
    {
        api.MapGet("/reconciliation/unreconciled", async ([AsParameters] PageParameters page, HttpRequest request, OpsBatchQueries queries, CancellationToken ct)
                => CsvResult<OpsBatchDto>.Wanted(request, page.Format)
                    ? new CsvResult<OpsBatchDto>(queries.ExportUnreconciled(), "unreconciled.csv")
                    : (await queries.UnreconciledAsync(page.Paging, ct)).ToHttp())
            .WithTags("Reconciliation")
            .WithSummary("Batches not reconciled and not yet in a bank run, oldest payment date first.");
    }

    private static void MapPayments(RouteGroupBuilder api)
    {
        api.MapGet("/payments/{id:long}", async (long id, OpsBatchQueries queries, CancellationToken ct) => (await queries.GetPaymentAsync(id, ct)).ToHttp())
            .WithTags("Payments");

        api.MapGet("/paid-away", async (string? client, ServiceTypeCode? serviceType, [AsParameters] PageParameters page, HttpRequest request, PaidQueries paid, CancellationToken ct)
                => CsvResult<PaidPaymentListItemDto>.Wanted(request, page.Format)
                    ? paid.Export(paid.Today, paid.Today, new PaidQuery(client, serviceType)).ToCsv("paid-away.csv")
                    : (await paid.PaidAwayAsync(new PaidQuery(client, serviceType), page.Paging, ct)).ToHttp())
            .WithTags("Paid")
            .WithSummary("Payments that became Success today (South African time).");

        api.MapGet("/paid-history", async ([FromQuery] DateOnly from, [FromQuery] DateOnly to, string? client, ServiceTypeCode? serviceType, [AsParameters] PageParameters page, HttpRequest request, PaidQueries paid, CancellationToken ct)
                => CsvResult<PaidPaymentListItemDto>.Wanted(request, page.Format)
                    ? paid.Export(from, to, new PaidQuery(client, serviceType)).ToCsv($"paid-{from:yyyyMMdd}-{to:yyyyMMdd}.csv")
                    : (await paid.HistoryAsync(from, to, new PaidQuery(client, serviceType), page.Paging, ct)).ToHttp())
            .WithTags("Paid")
            .WithSummary("Payments that became Success between from and to (inclusive, at most 31 days).");
    }

    private static void MapRejections(RouteGroupBuilder group)
    {
        group.MapGet("/", async (TransactionStatusCode? status, string? client, DateOnly? from, DateOnly? to, [AsParameters] PageParameters page, HttpRequest request, RejectionService service, CancellationToken ct)
                => CsvResult<RejectionListItemDto>.Wanted(request, page.Format)
                    ? service.Export(new RejectionQuery(status, client, from, to)).ToCsv("rejections.csv")
                    : (await service.ListAsync(new RejectionQuery(status, client, from, to), page.Paging, ct)).ToHttp())
            .WithSummary("Rejections by status: PendingUnreconciled (waiting for ops), Pending (confirmed, waiting for a bank run), Transmitted, Success, Rejected.");

        group.MapGet("/{id:long}", async (long id, RejectionService service, CancellationToken ct) => (await service.GetAsync(id, ct)).ToHttp());
        group.MapPut("/{id:long}/refund-account", async (long id, UpdateRefundAccountRequest body, CurrentStaff staff, RejectionService service, CancellationToken ct)
            => staff.Identity is not { } who ? ResultMapping.NoStaffIdentity() : (await service.UpdateRefundAccountAsync(id, body, who, ct)).ToHttp()).Write()
            .WithSummary("Correct the refund account (only while PendingUnreconciled).");
        group.MapPost("/{id:long}/confirm", async (long id, ConfirmRejectionRequest body, CurrentStaff staff, RejectionService service, CancellationToken ct)
            => staff.Identity is not { } who ? ResultMapping.NoStaffIdentity() : (await service.ConfirmAsync(id, body, who, ct)).ToHttp()).Write()
            .WithSummary("Confirm the refund: PendingUnreconciled -> Pending, picked up by the next bank run.");
    }
}
