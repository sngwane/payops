using Microsoft.EntityFrameworkCore;
using PayOps.Database.Lookups;

namespace PayOps.Payments.Data.Queries;

/// <summary>Read-only LINQ queries for the Payments API (replaces the legacy sp_PaymentBatch_Get / Paid Away / rejection procs).</summary>
public class PaymentQueries(PaymentsDbContext db)
{
    /// <summary>A client batch with its records, only when it belongs to the given secondary client.</summary>
    public Task<ClientBatchDto?> GetClientBatchAsync(string clientUniqueId, int paymentSummaryId, CancellationToken cancellationToken = default)
        => db.PaymentSummaries
            .AsNoTracking()
            .Where(s => s.Id == paymentSummaryId && s.SecondaryClient.UniqueClientId == clientUniqueId)
            .Select(s => new ClientBatchDto(
                s.Id,
                s.UniqueReference,
                s.SecondaryClient.UniqueClientId,
                s.SecondaryClient.Name,
                s.ServiceType.Name,
                s.PaymentDate,
                s.SequenceNumber,
                s.TotalRecords,
                s.TotalValue,
                s.ReconStatus.Name,
                s.PaymentBatch != null ? s.PaymentBatch.Reference : null,
                s.PaymentRecords
                    .OrderBy(r => r.Id)
                    .Select(r => new PaymentRecordDto(
                        r.Id, r.EmployeeCode, r.Surname, r.FirstName, r.BranchCode, r.AccountNumber, r.AccountType,
                        r.Amount, r.BankReference, r.TransactionStatus.Name, r.RejectionReason))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Payments that reached Success in [fromUtc, toUtc). "Paid Away" = today's range, "History" = any range.
    /// The paid date is taken from the status history.
    /// </summary>
    public async Task<IReadOnlyList<PaidPaymentDto>> GetPaidAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => await db.PaymentRecordStatusHistories
            .AsNoTracking()
            .Where(h => h.TransactionStatusId == (byte)TransactionStatusCode.Success
                        && h.PaymentRejectionId == null
                        && h.PaymentRecord.TransactionStatusId == (byte)TransactionStatusCode.Success
                        && h.ChangedDate >= fromUtc && h.ChangedDate < toUtc)
            .OrderBy(h => h.ChangedDate)
            .Select(h => new PaidPaymentDto(
                h.PaymentRecordId,
                h.PaymentRecord.PaymentSummary.SecondaryClient.UniqueClientId,
                h.PaymentRecord.PaymentSummary.SecondaryClient.Name,
                h.PaymentRecord.PaymentSummary.UniqueReference,
                h.PaymentRecord.EmployeeCode,
                h.PaymentRecord.Surname,
                h.PaymentRecord.Amount,
                h.ChangedDate))
            .ToListAsync(cancellationToken);

    /// <summary>Rejection refunds in a given status, e.g. Pending or PendingUnreconciled.</summary>
    public async Task<IReadOnlyList<RejectionDto>> GetRejectionsAsync(TransactionStatusCode status, CancellationToken cancellationToken = default)
        => await db.PaymentRejections
            .AsNoTracking()
            .Where(r => r.TransactionStatusId == (byte)status)
            .OrderBy(r => r.Id)
            .Select(r => new RejectionDto(
                r.Id,
                r.PaymentRecord.PaymentSummary.SecondaryClient.UniqueClientId,
                r.PaymentRecord.PaymentSummary.SecondaryClient.Name,
                r.PaymentRecord.PaymentSummary.PaymentBatch != null ? r.PaymentRecord.PaymentSummary.PaymentBatch.Reference : null,
                r.PaymentRecord.PaymentSummary.PaymentBatch != null ? r.PaymentRecord.PaymentSummary.PaymentBatch.FileName : null,
                r.PaymentBatch != null ? r.PaymentBatch.Reference : null,
                r.PaymentBatch != null ? r.PaymentBatch.FileName : null,
                r.PaymentRecord.PaymentSummary.ServiceType.Name,
                r.PaymentRecord.PaymentSummary.PaymentDate,
                r.AccountNumber,
                r.BranchCode,
                r.Amount,
                r.PaymentRecord.RejectionReason,
                r.RejectionReason))
            .ToListAsync(cancellationToken);
}
