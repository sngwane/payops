using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Data.Services;

/// <summary>Read-only LINQ queries. Client queries are scoped to the API key's client (a primary sees all its secondaries).</summary>
public sealed class BatchQueries(BatchFileDbContext db)
{
    public const int MaxPaymentsPageSize = 1000;

    public async Task<BatchResult<PagedResult<BatchSummaryDto>>> ListBatchesAsync(BatchCaller caller, BatchListQuery query, CancellationToken cancellationToken = default)
    {
        if (Paging(query.Page, query.PageSize, BatchListQuery.MaxPageSize) is { } error)
            return error;
        var batches = Owned(caller);
        if (query.From is { } from)
            batches = batches.Where(s => s.PaymentDate >= from);
        if (query.To is { } to)
            batches = batches.Where(s => s.PaymentDate <= to);
        if (!string.IsNullOrWhiteSpace(query.UniqueReference))
            batches = batches.Where(s => s.UniqueReference == query.UniqueReference);
        if (!string.IsNullOrWhiteSpace(query.ClientReference))
            batches = batches.Where(s => s.ClientReference == query.ClientReference);
        var total = await batches.CountAsync(cancellationToken);
        var items = await ProjectSummary(batches.OrderByDescending(s => s.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<BatchSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<BatchResult<BatchDetailDto>> GetBatchAsync(BatchCaller caller, int id, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (Paging(page, pageSize, MaxPaymentsPageSize) is { } error)
            return error;
        var batch = await ProjectSummary(Owned(caller).Where(s => s.Id == id)).SingleOrDefaultAsync(cancellationToken);
        if (batch is null)
            return BatchError.NotFound($"Batch {id} was not found.");
        var rejection = await db.PaymentSummaries.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new { s.RejectionAccountName, s.RejectionBranchCode, s.RejectionAccountNumber })
            .SingleAsync(cancellationToken);
        var records = db.PaymentRecords.AsNoTracking().Where(r => r.PaymentSummaryId == id);
        var statuses = await records.GroupBy(r => r.TransactionStatus.Name)
            .Select(g => new TransactionStatusCount(g.Key, g.Count(), g.Sum(r => r.Amount)))
            .ToListAsync(cancellationToken);
        var payments = await records.OrderBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new BatchPaymentDto(r.Id, r.EmployeeCode, r.Surname, r.FirstName, r.BranchCode, r.AccountNumber, r.AccountType, r.Amount,
                r.BankReference, r.TransactionStatus.Name, r.RejectionReason))
            .ToListAsync(cancellationToken);
        return new BatchDetailDto(batch, rejection.RejectionAccountName, rejection.RejectionBranchCode, rejection.RejectionAccountNumber, statuses,
            new PagedResult<BatchPaymentDto>(payments, page, pageSize, batch.Records));
    }

    public async Task<BatchResult<IReadOnlyList<RejectedPaymentDto>>> GetRejectionsAsync(BatchCaller caller, int id, CancellationToken cancellationToken = default)
    {
        if (!await Owned(caller).AnyAsync(s => s.Id == id, cancellationToken))
            return BatchError.NotFound($"Batch {id} was not found.");
        var rejected = await db.PaymentRecords.AsNoTracking()
            .Where(r => r.PaymentSummaryId == id && (r.TransactionStatusId == (byte)TransactionStatusCode.Rejected || r.PaymentRejection != null))
            .OrderBy(r => r.Id)
            .Select(r => new RejectedPaymentDto(r.Id, r.EmployeeCode, r.Surname, r.BranchCode, r.AccountNumber, r.Amount, r.BankReference,
                r.PaymentRejection != null ? r.PaymentRejection.RejectionReason : r.RejectionReason,
                r.PaymentRejection != null ? r.PaymentRejection.TransactionStatus.Name : null,
                r.PaymentRejection != null ? r.PaymentRejection.CreatedDate : r.ModifiedDate))
            .ToListAsync(cancellationToken);
        return rejected;
    }

    public async Task<BatchResult<PagedResult<BatchUploadDto>>> ListUploadsAsync(UploadListQuery query, CancellationToken cancellationToken = default)
    {
        if (Paging(query.Page, query.PageSize, BatchListQuery.MaxPageSize) is { } error)
            return error;
        var uploads = db.BatchUploads.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.UniqueClientId))
            uploads = uploads.Where(u => u.UniqueClientId == query.UniqueClientId
                                         || (u.SecondaryClient != null && u.SecondaryClient.UniqueClientId == query.UniqueClientId));
        if (query.IsAccepted is { } accepted)
            uploads = uploads.Where(u => u.IsAccepted == accepted);
        if (query.From is { } from)
            uploads = uploads.Where(u => u.ReceivedDate >= from);
        if (query.To is { } to)
            uploads = uploads.Where(u => u.ReceivedDate <= to);
        var total = await uploads.CountAsync(cancellationToken);
        var rows = await uploads.OrderByDescending(u => u.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(u => new { u.Id, u.UniqueClientId, u.SecondaryClientId, u.UniqueReference, u.Format, u.IsAccepted, u.RecordCount, u.TotalValue,
                u.ErrorsJson, u.PaymentSummaryId, u.ReceivedDate })
            .ToListAsync(cancellationToken);
        var items = rows.Select(u => new BatchUploadDto(u.Id, u.UniqueClientId, u.SecondaryClientId, u.UniqueReference, u.Format.ToString(), u.IsAccepted,
            u.RecordCount, u.TotalValue, u.ErrorsJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string[]>>(u.ErrorsJson),
            u.PaymentSummaryId, u.ReceivedDate)).ToList();
        return new PagedResult<BatchUploadDto>(items, query.Page, query.PageSize, total);
    }

    private IQueryable<PaymentSummary> Owned(BatchCaller caller)
        => caller.IsPrimary
            ? db.PaymentSummaries.AsNoTracking().Where(s => s.SecondaryClient.PrimaryClientId == caller.ClientId)
            : db.PaymentSummaries.AsNoTracking().Where(s => s.SecondaryClientId == caller.ClientId);

    private static IQueryable<BatchSummaryDto> ProjectSummary(IQueryable<PaymentSummary> batches)
        => batches.Select(s => new BatchSummaryDto(s.Id, s.UniqueReference, s.ClientReference, s.SecondaryClient.UniqueClientId, s.ServiceType.Name, s.PaymentDate,
            s.SequenceNumber, s.TotalRecords, s.TotalValue, s.FeeTotal, s.ReconStatus.Name, s.CreatedDate));

    internal static BatchError? Paging(int page, int pageSize, int maxPageSize)
    {
        var errors = new ValidationErrors();
        if (page < 1)
            errors.Add("page", "Must be 1 or more.");
        if (pageSize < 1 || pageSize > maxPageSize)
            errors.Add("pageSize", $"Must be between 1 and {maxPageSize}.");
        return errors.Any ? BatchError.Invalid(errors.ToDictionary()) : null;
    }
}
