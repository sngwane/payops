using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Payments.Data.Services;

/// <summary>Ops changes to a batch: reconciliation (funding), hold/release and comments. Each is recorded with who did it.</summary>
public sealed class BatchOperationsService(PaymentsDbContext db, StaffUserService users, OpsBatchQueries queries, TimeProvider clock)
{
    public const int MaxCommentLength = 900;

    public async Task<PaymentsResult<OpsBatchDetailDto>> SetReconStatusAsync(int id, SetReconStatusRequest request, StaffIdentity who, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Status is not { } status || !Enum.IsDefined(status))
            errors["status"] = ["status must be Reconciled, Unreconciled or Discrepancies."];
        var comment = PaymentsGuard.Text(errors, "comment", request.Comment, MaxCommentLength);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);

        var batch = await LoadAsync(id, ct);
        if (batch is null)
            return PaymentsError.NotFound($"Batch {id} was not found.");
        PaymentsGuard.ApplyRowVersion(db, batch, request.RowVersion, errors);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);
        if (batch.PaymentBatchId is { } bankRunId)
            return PaymentsError.Conflict($"Batch {id} is in bank run {bankRunId}; its reconciliation status can no longer change.");
        var current = (ReconStatusCode)batch.ReconStatusId;
        var target = request.Status!.Value;
        if (current == target)
            return PaymentsError.Conflict($"Batch {id} is already {target}.");

        var (userId, now) = (await users.GetUserIdAsync(who, ct), Now());
        batch.ReconStatusId = (byte)target;
        batch.ReconStatusChangedByUserId = userId;
        batch.ReconStatusChangedDate = now;
        Touch(batch, userId, now);
        AddComment(batch, $"Reconciliation {current} -> {target}: {comment}", userId, now);
        if (target == ReconStatusCode.Reconciled)
            await PaymentsGuard.QueueNotificationAsync(db, NotificationType.FundingConfirmation, batch.UniqueReference, batch, new
            {
                ClientName = batch.SecondaryClient.Name,
                batch.SecondaryClient.UniqueClientId,
                batch.UniqueReference,
                batch.ClientReference,
                batch.PaymentDate,
                batch.TotalRecords,
                batch.TotalValue,
                batch.FeeTotal
            }, ct);
        return await SaveAndReturnAsync(id, ct);
    }

    public async Task<PaymentsResult<OpsBatchDetailDto>> HoldAsync(int id, HoldRequest request, StaffIdentity who, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var reason = PaymentsGuard.Text(errors, "reason", request.Reason, 500);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);

        var batch = await LoadAsync(id, ct);
        if (batch is null)
            return PaymentsError.NotFound($"Batch {id} was not found.");
        PaymentsGuard.ApplyRowVersion(db, batch, request.RowVersion, errors);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);
        if (batch.PaymentBatchId is { } bankRunId)
            return PaymentsError.Conflict($"Batch {id} is already in bank run {bankRunId} and can't be put on hold.");
        if (batch.IsOnHold)
            return PaymentsError.Conflict($"Batch {id} is already on hold.");

        var (userId, now) = (await users.GetUserIdAsync(who, ct), Now());
        batch.IsOnHold = true;
        batch.HoldReason = reason;
        batch.HeldByUserId = userId;
        batch.HeldDate = now;
        Touch(batch, userId, now);
        AddComment(batch, $"Put on hold: {reason}", userId, now);
        return await SaveAndReturnAsync(id, ct);
    }

    public async Task<PaymentsResult<OpsBatchDetailDto>> ReleaseAsync(int id, ReleaseRequest request, StaffIdentity who, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var comment = PaymentsGuard.Text(errors, "comment", request.Comment, MaxCommentLength);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);

        var batch = await LoadAsync(id, ct);
        if (batch is null)
            return PaymentsError.NotFound($"Batch {id} was not found.");
        PaymentsGuard.ApplyRowVersion(db, batch, request.RowVersion, errors);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);
        if (!batch.IsOnHold)
            return PaymentsError.Conflict($"Batch {id} is not on hold.");

        var (userId, now) = (await users.GetUserIdAsync(who, ct), Now());
        batch.IsOnHold = false;
        batch.HoldReason = null;
        batch.HeldByUserId = null;
        batch.HeldDate = null;
        Touch(batch, userId, now);
        AddComment(batch, $"Released from hold: {comment}", userId, now);
        return await SaveAndReturnAsync(id, ct);
    }

    public async Task<PaymentsResult<CommentDto>> AddCommentAsync(int id, AddCommentRequest request, StaffIdentity who, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var text = PaymentsGuard.Text(errors, "comment", request.Comment, 1000);
        if (errors.Count > 0)
            return PaymentsError.Invalid(errors);
        if (!await db.PaymentSummaries.AnyAsync(s => s.Id == id, ct))
            return PaymentsError.NotFound($"Batch {id} was not found.");

        var userId = await users.GetUserIdAsync(who, ct);
        var comment = new PaymentSummaryComment { PaymentSummaryId = id, Comment = text!, CreatedByUserId = userId, CreatedDate = Now() };
        db.PaymentSummaryComments.Add(comment);
        await db.SaveChangesAsync(ct);
        return new CommentDto(comment.Id, comment.Comment, who.Username, comment.CreatedDate);
    }

    private Task<PaymentSummary?> LoadAsync(int id, CancellationToken ct)
        => db.PaymentSummaries.Include(s => s.SecondaryClient).FirstOrDefaultAsync(s => s.Id == id, ct);

    private async Task<PaymentsResult<OpsBatchDetailDto>> SaveAndReturnAsync(int id, CancellationToken ct)
        => await PaymentsGuard.SaveAsync(db, ct) is { } error ? error : await queries.GetAsync(id, ct);

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private static void Touch(PaymentSummary batch, int userId, DateTime now)
    {
        batch.ModifiedByUserId = userId;
        batch.ModifiedDate = now;
    }

    private void AddComment(PaymentSummary batch, string text, int userId, DateTime now)
        => db.PaymentSummaryComments.Add(new PaymentSummaryComment
        {
            PaymentSummaryId = batch.Id,
            Comment = text.Length <= 1000 ? text : text[..1000],
            CreatedByUserId = userId,
            CreatedDate = now
        });
}
