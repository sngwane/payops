using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Payments.Data.Services;

/// <summary>Read side of the ops back office: batch search, detail, payments, comments and change history.</summary>
public sealed class OpsBatchQueries(PaymentsDbContext db)
{
    private static readonly Expression<Func<PaymentSummary, OpsBatchDto>> ToListItem = s => new OpsBatchDto(
        s.Id,
        s.UniqueReference,
        s.ClientReference,
        s.SecondaryClientId,
        s.SecondaryClient.UniqueClientId,
        s.SecondaryClient.Name,
        s.SecondaryClient.PrimaryClient.UniqueClientId,
        (ServiceTypeCode)s.ServiceTypeId,
        s.PaymentDate,
        s.SequenceNumber,
        s.TotalRecords,
        s.TotalValue,
        s.FeeTotal,
        (ReconStatusCode)s.ReconStatusId,
        s.IsOnHold,
        s.PaymentBatchId,
        s.PaymentBatch != null ? s.PaymentBatch.Reference : null,
        s.CreatedDate);

    private static readonly Expression<Func<PaymentRecord, OpsPaymentDto>> ToPayment = r => new OpsPaymentDto(
        r.Id, r.EmployeeCode, r.Surname, r.FirstName, r.Initials, r.IdOrPassport, r.BranchCode, r.AccountNumber, r.AccountType,
        r.Amount, (TransactionStatusCode)r.TransactionStatusId, r.BankReference, r.BankFileReference, r.RejectionReason, r.Comment);

    public async Task<PaymentsResult<PagedResult<OpsBatchDto>>> ListAsync(OpsBatchQuery query, Paging paging, CancellationToken ct = default)
    {
        var error = PaymentsGuard.CheckPaging(paging) ?? CheckDates(query);
        if (error is not null)
            return error;
        return await PaymentsGuard.PageAsync(Ordered(Filter(query)).Select(ToListItem), paging, ct);
    }

    public PaymentsResult<IAsyncEnumerable<OpsBatchDto>> Export(OpsBatchQuery query)
        => CheckDates(query) is { } error
            ? error
            : PaymentsResult<IAsyncEnumerable<OpsBatchDto>>.Ok(Ordered(Filter(query)).Select(ToListItem).AsAsyncEnumerable());

    /// <summary>Batches waiting for funding: not reconciled and not yet in a bank run, oldest payment date first.</summary>
    public async Task<PaymentsResult<PagedResult<OpsBatchDto>>> UnreconciledAsync(Paging paging, CancellationToken ct = default)
        => PaymentsGuard.CheckPaging(paging) is { } error
            ? error
            : await PaymentsGuard.PageAsync(Unreconciled().Select(ToListItem), paging, ct);

    public IAsyncEnumerable<OpsBatchDto> ExportUnreconciled() => Unreconciled().Select(ToListItem).AsAsyncEnumerable();

    public async Task<PaymentsResult<OpsBatchDetailDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var batch = await db.PaymentSummaries.AsNoTracking().Where(s => s.Id == id).Select(s => new
        {
            s.Id, s.UniqueReference, s.ClientReference, s.LegacyId, s.SecondaryClientId,
            ClientUniqueId = s.SecondaryClient.UniqueClientId, ClientName = s.SecondaryClient.Name,
            s.SecondaryClient.PrimaryClientId, PrimaryUniqueId = s.SecondaryClient.PrimaryClient.UniqueClientId,
            PrimaryName = s.SecondaryClient.PrimaryClient.Name,
            s.ServiceTypeId, s.PaymentDate, s.SequenceNumber, s.TotalRecords, s.TotalValue, s.FeeTotal,
            s.RejectionAccountName, s.RejectionBranchCode, s.RejectionAccountNumber,
            s.ReconStatusId, ReconBy = s.ReconStatusChangedByUser != null ? s.ReconStatusChangedByUser.Username : null, s.ReconStatusChangedDate,
            s.IsOnHold, s.HoldReason, HeldBy = s.HeldByUser != null ? s.HeldByUser.Username : null, s.HeldDate,
            s.PaymentBatchId, BankRunReference = s.PaymentBatch != null ? s.PaymentBatch.Reference : null,
            BankRunFileName = s.PaymentBatch != null ? s.PaymentBatch.FileName : null,
            s.CreatedDate, s.ModifiedDate, s.RowVersion
        }).FirstOrDefaultAsync(ct);
        if (batch is null)
            return PaymentsError.NotFound($"Batch {id} was not found.");

        var statuses = await db.PaymentRecords.AsNoTracking()
            .Where(r => r.PaymentSummaryId == id)
            .GroupBy(r => r.TransactionStatusId)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(r => r.Amount) })
            .ToListAsync(ct);
        var rejections = await db.PaymentRejections.CountAsync(r => r.PaymentRecord.PaymentSummaryId == id, ct);

        return new OpsBatchDetailDto(
            batch.Id, batch.UniqueReference, batch.ClientReference, batch.LegacyId, batch.SecondaryClientId, batch.ClientUniqueId,
            batch.ClientName, batch.PrimaryClientId, batch.PrimaryUniqueId, batch.PrimaryName, (ServiceTypeCode)batch.ServiceTypeId,
            batch.PaymentDate, batch.SequenceNumber, batch.TotalRecords, batch.TotalValue, batch.FeeTotal,
            batch.RejectionAccountName, batch.RejectionBranchCode, batch.RejectionAccountNumber,
            (ReconStatusCode)batch.ReconStatusId, batch.ReconBy, batch.ReconStatusChangedDate,
            batch.IsOnHold, batch.HoldReason, batch.HeldBy, batch.HeldDate,
            batch.PaymentBatchId, batch.BankRunReference, batch.BankRunFileName, batch.CreatedDate, batch.ModifiedDate,
            rejections,
            statuses.OrderBy(s => s.Status).Select(s => new StatusCountDto((TransactionStatusCode)s.Status, s.Count, s.Amount)).ToList(),
            Convert.ToBase64String(batch.RowVersion));
    }

    public async Task<PaymentsResult<PagedResult<OpsPaymentDto>>> PaymentsAsync(int batchId, OpsPaymentQuery query, Paging paging, CancellationToken ct = default)
    {
        if (PaymentsGuard.CheckPaging(paging) is { } error)
            return error;
        if (!await db.PaymentSummaries.AnyAsync(s => s.Id == batchId, ct))
            return PaymentsError.NotFound($"Batch {batchId} was not found.");
        return await PaymentsGuard.PageAsync(Payments(batchId, query).Select(ToPayment), paging, ct);
    }

    public async Task<PaymentsResult<IAsyncEnumerable<OpsPaymentDto>>> ExportPaymentsAsync(int batchId, OpsPaymentQuery query, CancellationToken ct = default)
        => await db.PaymentSummaries.AnyAsync(s => s.Id == batchId, ct)
            ? PaymentsResult<IAsyncEnumerable<OpsPaymentDto>>.Ok(Payments(batchId, query).Select(ToPayment).AsAsyncEnumerable())
            : PaymentsError.NotFound($"Batch {batchId} was not found.");

    public async Task<PaymentsResult<PaymentDetailDto>> GetPaymentAsync(long id, CancellationToken ct = default)
    {
        var payment = await db.PaymentRecords.AsNoTracking().Where(r => r.Id == id).Select(r => new
        {
            r.Id, r.PaymentSummaryId, BatchReference = r.PaymentSummary.UniqueReference,
            ClientUniqueId = r.PaymentSummary.SecondaryClient.UniqueClientId, ClientName = r.PaymentSummary.SecondaryClient.Name,
            r.PaymentSummary.ServiceTypeId, r.PaymentSummary.PaymentDate,
            r.EmployeeCode, r.Surname, r.FirstName, r.Initials, r.IdOrPassport, r.Cell, r.Email, r.BranchCode, r.AccountNumber,
            r.AccountType, r.Amount, r.TransactionStatusId, r.BankReference, r.BankFileReference, r.RejectionReason, r.Comment,
            RejectionId = r.PaymentRejection != null ? (long?)r.PaymentRejection.Id : null,
            RejectionStatus = r.PaymentRejection != null ? (byte?)r.PaymentRejection.TransactionStatusId : null
        }).FirstOrDefaultAsync(ct);
        if (payment is null)
            return PaymentsError.NotFound($"Payment {id} was not found.");

        var history = await db.PaymentRecordStatusHistories.AsNoTracking()
            .Where(h => h.PaymentRecordId == id)
            .OrderBy(h => h.ChangedDate).ThenBy(h => h.Id)
            .Select(h => new StatusHistoryDto((TransactionStatusCode)h.TransactionStatusId, h.ChangedDate, h.Reason, h.PaymentBatchId,
                h.PaymentRejectionId, h.ChangedByUser != null ? h.ChangedByUser.Username : null))
            .ToListAsync(ct);

        return new PaymentDetailDto(
            payment.Id, payment.PaymentSummaryId, payment.BatchReference, payment.ClientUniqueId, payment.ClientName,
            (ServiceTypeCode)payment.ServiceTypeId, payment.PaymentDate, payment.EmployeeCode, payment.Surname, payment.FirstName,
            payment.Initials, payment.IdOrPassport, payment.Cell, payment.Email, payment.BranchCode, payment.AccountNumber,
            payment.AccountType, payment.Amount, (TransactionStatusCode)payment.TransactionStatusId, payment.BankReference,
            payment.BankFileReference, payment.RejectionReason, payment.Comment, payment.RejectionId,
            (TransactionStatusCode?)payment.RejectionStatus, history);
    }

    public async Task<PaymentsResult<IReadOnlyList<CommentDto>>> CommentsAsync(int batchId, CancellationToken ct = default)
    {
        if (!await db.PaymentSummaries.AnyAsync(s => s.Id == batchId, ct))
            return PaymentsError.NotFound($"Batch {batchId} was not found.");
        return PaymentsResult<IReadOnlyList<CommentDto>>.Ok(await Comments(batchId).ToListAsync(ct));
    }

    /// <summary>
    /// Who changed what: field changes between consecutive versions in the temporal history (the version's ModifiedByUserId
    /// is who made the change), merged with the batch comments.
    /// </summary>
    public async Task<PaymentsResult<IReadOnlyList<BatchChangeDto>>> HistoryAsync(int batchId, CancellationToken ct = default)
    {
        var versions = await db.PaymentSummaries.TemporalAll().AsNoTracking()
            .Where(s => s.Id == batchId)
            .OrderBy(s => EF.Property<DateTime>(s, "ValidFrom"))
            .Select(s => new SummaryVersion(EF.Property<DateTime>(s, "ValidFrom"), s.ReconStatusId, s.IsOnHold, s.HoldReason, s.PaymentBatchId, s.ModifiedByUserId, s.CreatedByUserId))
            .ToListAsync(ct);
        if (versions.Count == 0)
            return PaymentsError.NotFound($"Batch {batchId} was not found.");

        var userIds = versions.Select(v => v.ModifiedByUserId).Append(versions[0].CreatedByUserId).OfType<int>().Distinct().ToList();
        var users = await db.AppUsers.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        string? Name(int? userId) => userId is { } uid && users.TryGetValue(uid, out var name) ? name : null;
        string? By(SummaryVersion v) => Name(v.ModifiedByUserId);

        var changes = new List<BatchChangeDto> { new(versions[0].From, "Created", null, ((ReconStatusCode)versions[0].ReconStatusId).ToString(), Name(versions[0].CreatedByUserId)) };
        for (var i = 1; i < versions.Count; i++)
        {
            var (before, after) = (versions[i - 1], versions[i]);
            void Add(string field, string? oldValue, string? newValue)
            {
                if (oldValue != newValue)
                    changes.Add(new BatchChangeDto(after.From, field, oldValue, newValue, By(after)));
            }
            Add("ReconStatus", ((ReconStatusCode)before.ReconStatusId).ToString(), ((ReconStatusCode)after.ReconStatusId).ToString());
            Add("OnHold", before.IsOnHold.ToString(), after.IsOnHold.ToString());
            Add("HoldReason", before.HoldReason, after.HoldReason);
            Add("BankRun", before.PaymentBatchId?.ToString(), after.PaymentBatchId?.ToString());
        }

        var comments = await Comments(batchId).ToListAsync(ct);
        changes.AddRange(comments.Select(c => new BatchChangeDto(c.CreatedDate, "Comment", null, c.Comment, c.CreatedBy)));
        return PaymentsResult<IReadOnlyList<BatchChangeDto>>.Ok(changes.OrderBy(c => c.ChangedDate).ToList());
    }

    private sealed record SummaryVersion(DateTime From, byte ReconStatusId, bool IsOnHold, string? HoldReason, int? PaymentBatchId, int? ModifiedByUserId, int? CreatedByUserId);

    private IQueryable<CommentDto> Comments(int batchId)
        => db.PaymentSummaryComments.AsNoTracking()
            .Where(c => c.PaymentSummaryId == batchId)
            .OrderBy(c => c.CreatedDate).ThenBy(c => c.Id)
            .Select(c => new CommentDto(c.Id, c.Comment, c.CreatedByUser != null ? c.CreatedByUser.Username : null, c.CreatedDate));

    private IQueryable<PaymentRecord> Payments(int batchId, OpsPaymentQuery query)
    {
        var records = db.PaymentRecords.AsNoTracking().Where(r => r.PaymentSummaryId == batchId);
        if (query.Status is { } status)
            records = records.Where(r => r.TransactionStatusId == (byte)status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            records = records.Where(r => r.EmployeeCode.Contains(search) || r.Surname.Contains(search) || r.AccountNumber == search);
        }
        return records.OrderBy(r => r.Id);
    }

    private IQueryable<PaymentSummary> Unreconciled()
        => db.PaymentSummaries.AsNoTracking()
            .Where(s => s.ReconStatusId != (byte)ReconStatusCode.Reconciled && s.PaymentBatchId == null)
            .OrderBy(s => s.PaymentDate).ThenBy(s => s.Id);

    private static PaymentsError? CheckDates(OpsBatchQuery query)
        => query.PaymentDateFrom > query.PaymentDateTo
            ? PaymentsError.Invalid("paymentDateTo", "paymentDateTo must be on or after paymentDateFrom.")
            : null;

    private static IOrderedQueryable<PaymentSummary> Ordered(IQueryable<PaymentSummary> batches)
        => batches.OrderByDescending(s => s.PaymentDate).ThenByDescending(s => s.Id);

    private IQueryable<PaymentSummary> Filter(OpsBatchQuery q)
    {
        var batches = db.PaymentSummaries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Client))
        {
            var client = q.Client.Trim();
            batches = batches.Where(s => s.SecondaryClient.UniqueClientId.Contains(client) || s.SecondaryClient.Name.Contains(client));
        }
        if (q.PrimaryClientId is { } primaryId)
            batches = batches.Where(s => s.SecondaryClient.PrimaryClientId == primaryId);
        if (q.SecondaryClientId is { } secondaryId)
            batches = batches.Where(s => s.SecondaryClientId == secondaryId);
        if (!string.IsNullOrWhiteSpace(q.ClientReference))
            batches = batches.Where(s => s.ClientReference == q.ClientReference);
        if (!string.IsNullOrWhiteSpace(q.UniqueReference))
            batches = batches.Where(s => s.UniqueReference == q.UniqueReference);
        if (q.ServiceType is { } serviceType)
            batches = batches.Where(s => s.ServiceTypeId == (byte)serviceType);
        if (q.PaymentDateFrom is { } from)
            batches = batches.Where(s => s.PaymentDate >= from);
        if (q.PaymentDateTo is { } to)
            batches = batches.Where(s => s.PaymentDate <= to);
        if (q.ReconStatus is { } recon)
            batches = batches.Where(s => s.ReconStatusId == (byte)recon);
        if (q.OnHold is { } onHold)
            batches = batches.Where(s => s.IsOnHold == onHold);
        if (q.InBankRun is { } inBankRun)
            batches = batches.Where(s => (s.PaymentBatchId != null) == inBankRun);
        if (q.HasRejections is { } hasRejections)
            batches = batches.Where(s => s.PaymentRecords.Any(r => r.PaymentRejection != null) == hasRejections);
        return batches;
    }
}
