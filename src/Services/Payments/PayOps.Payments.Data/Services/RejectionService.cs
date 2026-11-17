using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Payments.Data.Services;

/// <summary>
/// Rejection refunds: a rejected payment lands as PendingUnreconciled; ops correct the refund account if needed and confirm,
/// which makes it Pending for the next bank run (2d).
/// </summary>
public sealed partial class RejectionService(PaymentsDbContext db, StaffUserService users, OpsBatchQueries queries, TimeProvider clock)
{
    private static readonly Expression<Func<PaymentRejection, RejectionListItemDto>> ToItem = r => new RejectionListItemDto(
        r.Id,
        r.PaymentRecordId,
        r.PaymentRecord.PaymentSummaryId,
        r.PaymentRecord.PaymentSummary.UniqueReference,
        r.PaymentRecord.PaymentSummary.SecondaryClient.UniqueClientId,
        r.PaymentRecord.PaymentSummary.SecondaryClient.Name,
        (ServiceTypeCode)r.PaymentRecord.PaymentSummary.ServiceTypeId,
        r.PaymentRecord.PaymentSummary.PaymentDate,
        r.PaymentRecord.PaymentSummary.PaymentBatch != null ? r.PaymentRecord.PaymentSummary.PaymentBatch.Reference : null,
        r.PaymentBatch != null ? r.PaymentBatch.Reference : null,
        r.AccountName,
        r.BranchCode,
        r.AccountNumber,
        r.Amount,
        (TransactionStatusCode)r.TransactionStatusId,
        r.PaymentRecord.RejectionReason,
        r.RejectionReason,
        r.CreatedDate,
        r.ConfirmedByUser != null ? r.ConfirmedByUser.Username : null,
        r.ConfirmedDate);

    public async Task<PaymentsResult<PagedResult<RejectionListItemDto>>> ListAsync(RejectionQuery query, Paging paging, CancellationToken ct = default)
    {
        if ((PaymentsGuard.CheckPaging(paging) ?? CheckDates(query)) is { } error)
            return error;
        return await PaymentsGuard.PageAsync(Filter(query).Select(ToItem), paging, ct);
    }

    public PaymentsResult<IAsyncEnumerable<RejectionListItemDto>> Export(RejectionQuery query)
        => CheckDates(query) is { } error
            ? error
            : PaymentsResult<IAsyncEnumerable<RejectionListItemDto>>.Ok(Filter(query).Select(ToItem).AsAsyncEnumerable());

    public async Task<PaymentsResult<RejectionDetailDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var rejection = await db.PaymentRejections.AsNoTracking().Where(r => r.Id == id).Select(r => new
        {
            r.Id, r.PaymentRecordId, r.TransactionStatusId, r.AccountName, r.BranchCode, r.AccountNumber, r.Amount, r.BankReference,
            r.BankFileReference, r.RejectionReason, r.PaymentBatchId, BankRunReference = r.PaymentBatch != null ? r.PaymentBatch.Reference : null,
            r.CreatedDate, r.ModifiedDate, ConfirmedBy = r.ConfirmedByUser != null ? r.ConfirmedByUser.Username : null, r.ConfirmedDate, r.RowVersion
        }).FirstOrDefaultAsync(ct);
        if (rejection is null)
            return PaymentsError.NotFound($"Rejection {id} was not found.");

        var payment = await queries.GetPaymentAsync(rejection.PaymentRecordId, ct);
        if (!payment.IsSuccess)
            return payment.Error!;
        return new RejectionDetailDto(
            rejection.Id, (TransactionStatusCode)rejection.TransactionStatusId, rejection.AccountName, rejection.BranchCode,
            rejection.AccountNumber, rejection.Amount, rejection.BankReference, rejection.BankFileReference, rejection.RejectionReason,
            rejection.PaymentBatchId, rejection.BankRunReference, rejection.CreatedDate, rejection.ModifiedDate, rejection.ConfirmedBy,
            rejection.ConfirmedDate, Convert.ToBase64String(rejection.RowVersion), payment.Value!);
    }

    public async Task<PaymentsResult<RejectionDetailDto>> UpdateRefundAccountAsync(long id, UpdateRefundAccountRequest request, StaffIdentity who, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var name = PaymentsGuard.Text(errors, "accountName", request.AccountName, 100);
        var branch = request.BranchCode?.Trim();
        if (branch is null || !BranchCode().IsMatch(branch))
            errors["branchCode"] = ["branchCode must be 6 digits."];
        var account = request.AccountNumber?.Trim();
        if (account is null || !AccountNumber().IsMatch(account))
            errors["accountNumber"] = ["accountNumber must be 1 to 20 digits."];
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);

        var rejection = await db.PaymentRejections.Include(r => r.PaymentRecord).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rejection is null)
            return PaymentsError.NotFound($"Rejection {id} was not found.");
        PaymentsGuard.ApplyRowVersion(db, rejection, request.RowVersion, errors);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);
        if (rejection.TransactionStatusId != (byte)TransactionStatusCode.PendingUnreconciled)
            return PaymentsError.Conflict($"Rejection {id} is {(TransactionStatusCode)rejection.TransactionStatusId}; the refund account can only change while it is PendingUnreconciled.");

        var userId = await users.GetUserIdAsync(who, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        // Audit trail: the change and who made it are kept as a batch comment (shown in the batch history).
        db.PaymentSummaryComments.Add(new PaymentSummaryComment
        {
            PaymentSummaryId = rejection.PaymentRecord.PaymentSummaryId,
            Comment = Clip($"Refund account for rejection {id} changed from {rejection.AccountName} {rejection.BranchCode} {rejection.AccountNumber} to {name} {branch} {account}", 1000),
            CreatedByUserId = userId,
            CreatedDate = now
        });
        rejection.AccountName = name!;
        rejection.BranchCode = branch!;
        rejection.AccountNumber = account!;
        rejection.ModifiedDate = now;
        return await PaymentsGuard.SaveAsync(db, ct) is { } saveError ? saveError : await GetAsync(id, ct);
    }

    public async Task<PaymentsResult<RejectionDetailDto>> ConfirmAsync(long id, ConfirmRejectionRequest request, StaffIdentity who, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var comment = PaymentsGuard.Text(errors, "comment", request.Comment, 500);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);

        var rejection = await db.PaymentRejections
            .Include(r => r.PaymentRecord).ThenInclude(p => p.PaymentSummary).ThenInclude(s => s.SecondaryClient)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rejection is null)
            return PaymentsError.NotFound($"Rejection {id} was not found.");
        PaymentsGuard.ApplyRowVersion(db, rejection, request.RowVersion, errors);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);
        if (rejection.TransactionStatusId != (byte)TransactionStatusCode.PendingUnreconciled)
            return PaymentsError.Conflict($"Rejection {id} is {(TransactionStatusCode)rejection.TransactionStatusId}; only PendingUnreconciled rejections can be confirmed.");

        var userId = await users.GetUserIdAsync(who, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        rejection.TransactionStatusId = (byte)TransactionStatusCode.Pending;
        rejection.ConfirmedByUserId = userId;
        rejection.ConfirmedDate = now;
        rejection.ModifiedDate = now;
        db.PaymentRecordStatusHistories.Add(new PaymentRecordStatusHistory
        {
            PaymentRecordId = rejection.PaymentRecordId,
            PaymentRejectionId = rejection.Id,
            TransactionStatusId = (byte)TransactionStatusCode.Pending,
            Reason = Clip($"Refund confirmed: {comment}", 500),
            ChangedByUserId = userId,
            ChangedDate = now
        });
        var payment = rejection.PaymentRecord;
        var batch = payment.PaymentSummary;
        await PaymentsGuard.QueueNotificationAsync(db, NotificationType.RejectionConfirmation, rejection.Id.ToString(), batch, new
        {
            ClientName = batch.SecondaryClient.Name,
            batch.SecondaryClient.UniqueClientId,
            batch.UniqueReference,
            batch.ClientReference,
            RejectionId = rejection.Id,
            payment.EmployeeCode,
            payment.Surname,
            payment.RejectionReason,
            rejection.AccountName,
            rejection.BranchCode,
            rejection.AccountNumber,
            rejection.Amount
        }, ct);
        return await PaymentsGuard.SaveAsync(db, ct) is { } saveError ? saveError : await GetAsync(id, ct);
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    private static PaymentsError? CheckDates(RejectionQuery query)
        => query.From > query.To ? PaymentsError.Invalid("to", "to must be on or after from.") : null;

    private IQueryable<PaymentRejection> Filter(RejectionQuery query)
    {
        var rejections = db.PaymentRejections.AsNoTracking();
        if (query.Status is { } status)
            rejections = rejections.Where(r => r.TransactionStatusId == (byte)status);
        if (!string.IsNullOrWhiteSpace(query.Client))
        {
            var client = query.Client.Trim();
            rejections = rejections.Where(r => r.PaymentRecord.PaymentSummary.SecondaryClient.UniqueClientId.Contains(client)
                                               || r.PaymentRecord.PaymentSummary.SecondaryClient.Name.Contains(client));
        }
        if (query.From is { } from)
        {
            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            rejections = rejections.Where(r => r.CreatedDate >= fromDate);
        }
        if (query.To is { } to)
        {
            var toDate = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            rejections = rejections.Where(r => r.CreatedDate < toDate);
        }
        return rejections.OrderByDescending(r => r.CreatedDate).ThenByDescending(r => r.Id);
    }

    [GeneratedRegex(@"\A[0-9]{6}\z")]
    private static partial Regex BranchCode();

    [GeneratedRegex(@"\A[0-9]{1,20}\z")]
    private static partial Regex AccountNumber();
}
