using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Payments.Data.Services;

/// <summary>Paid Away (today) and Paid History (date range): payments whose status became Success, by local (SAST) day.</summary>
public sealed class PaidQueries(PaymentsDbContext db, IOptions<PaymentsOptions> options, TimeProvider clock)
{
    private static readonly Expression<Func<PaymentRecordStatusHistory, PaidPaymentListItemDto>> ToItem = h => new PaidPaymentListItemDto(
        h.PaymentRecordId,
        h.PaymentRecord.PaymentSummaryId,
        h.PaymentRecord.PaymentSummary.UniqueReference,
        h.PaymentRecord.PaymentSummary.SecondaryClient.UniqueClientId,
        h.PaymentRecord.PaymentSummary.SecondaryClient.Name,
        (ServiceTypeCode)h.PaymentRecord.PaymentSummary.ServiceTypeId,
        h.PaymentRecord.EmployeeCode,
        h.PaymentRecord.Surname,
        h.PaymentRecord.BranchCode,
        h.PaymentRecord.AccountNumber,
        h.PaymentRecord.Amount,
        h.PaymentRecord.BankReference,
        h.ChangedDate);

    private TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

    public Task<PaymentsResult<PaidListDto>> PaidAwayAsync(PaidQuery query, Paging paging, CancellationToken ct = default)
        => HistoryAsync(Today, Today, query, paging, ct);

    public async Task<PaymentsResult<PaidListDto>> HistoryAsync(DateOnly from, DateOnly to, PaidQuery query, Paging paging, CancellationToken ct = default)
    {
        if ((PaymentsGuard.CheckPaging(paging) ?? CheckRange(from, to)) is { } error)
            return error;
        var paid = Filter(from, to, query);
        var total = await paid.CountAsync(ct);
        var amount = await paid.SumAsync(h => (decimal?)h.PaymentRecord.Amount, ct) ?? 0m;
        var items = await paid.OrderBy(h => h.ChangedDate).ThenBy(h => h.Id)
            .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)
            .Select(ToItem).ToListAsync(ct);
        return new PaidListDto(items, paging.Page, paging.PageSize, total, amount, from, to);
    }

    public PaymentsResult<IAsyncEnumerable<PaidPaymentListItemDto>> Export(DateOnly from, DateOnly to, PaidQuery query)
        => CheckRange(from, to) is { } error
            ? error
            : PaymentsResult<IAsyncEnumerable<PaidPaymentListItemDto>>.Ok(
                Filter(from, to, query).OrderBy(h => h.ChangedDate).ThenBy(h => h.Id).Select(ToItem).AsAsyncEnumerable());

    private PaymentsError? CheckRange(DateOnly from, DateOnly to)
    {
        if (to < from)
            return PaymentsError.Invalid("to", "to must be on or after from.");
        var max = options.Value.MaxHistoryDays;
        return to.DayNumber - from.DayNumber + 1 > max
            ? PaymentsError.Invalid("to", $"A request can cover at most {max} days.")
            : null;
    }

    private IQueryable<PaymentRecordStatusHistory> Filter(DateOnly from, DateOnly to, PaidQuery query)
    {
        var zone = Zone;
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var paid = db.PaymentRecordStatusHistories.AsNoTracking()
            .Where(h => h.TransactionStatusId == (byte)TransactionStatusCode.Success
                        && h.PaymentRejectionId == null
                        && h.PaymentRecord.TransactionStatusId == (byte)TransactionStatusCode.Success
                        && h.ChangedDate >= fromUtc && h.ChangedDate < toUtc);
        if (!string.IsNullOrWhiteSpace(query.Client))
        {
            var client = query.Client.Trim();
            paid = paid.Where(h => h.PaymentRecord.PaymentSummary.SecondaryClient.UniqueClientId.Contains(client)
                                   || h.PaymentRecord.PaymentSummary.SecondaryClient.Name.Contains(client));
        }
        if (query.ServiceType is { } serviceType)
            paid = paid.Where(h => h.PaymentRecord.PaymentSummary.ServiceTypeId == (byte)serviceType);
        return paid;
    }
}
