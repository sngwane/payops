using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Data.Services;

/// <summary>Validates client batch uploads and stores accepted batches (summary, payments, contacts, outbox message, upload log) in one transaction.</summary>
public sealed class BatchSubmissionService(BatchFileDbContext db, TimeProvider clock, IOptions<BatchFileOptions> options)
{
    private readonly BatchFileOptions _options = options.Value;

    public async Task<BatchResult<BatchAcceptedDto>> SubmitAsync(BatchCaller caller, BatchSubmission batch, BatchUploadFormat format, byte[] payloadHash,
        CancellationToken cancellationToken = default)
    {
        var (check, error) = await CheckAsync(caller, batch, payloadHash, cancellationToken);
        if (error is not null)
        {
            await LogRejectedAsync(caller, check?.SecondaryClientId, batch, format, payloadHash, error, cancellationToken);
            return error;
        }

        var summary = BuildSummary(batch, check!);
        db.BatchUploads.Add(NewUpload(caller, check!.SecondaryClientId, batch, format, payloadHash, summary));
        db.NotificationMessages.Add(new NotificationMessage
        {
            Type = NotificationType.UploadConfirmation,
            DeduplicationKey = $"{NotificationType.UploadConfirmation}:{summary.UniqueReference}",
            SecondaryClientId = check.SecondaryClientId,
            PaymentSummary = summary,
            PayloadJson = JsonSerializer.Serialize(new
            {
                ClientName = check.ClientName,
                batch.Header.UniqueClientId,
                summary.UniqueReference,
                summary.ClientReference,
                PaymentDate = summary.PaymentDate.ToString("yyyy-MM-dd"),
                UploadDate = clock.GetUtcNow().UtcDateTime,
                Records = summary.TotalRecords,
                summary.TotalValue,
                summary.FeeTotal
            })
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            var duplicate = await FindDuplicateAsync(check.SecondaryClientId, batch.Header, payloadHash, cancellationToken)
                ?? BatchError.Conflict("The batch was submitted at the same time by another request.");
            await LogRejectedAsync(caller, check.SecondaryClientId, batch, format, payloadHash, duplicate, cancellationToken);
            return duplicate;
        }

        return new BatchAcceptedDto(summary.Id, summary.UniqueReference, summary.ClientReference, batch.Header.UniqueClientId, summary.PaymentDate, check.ServiceType.ToString(),
            summary.TotalRecords, summary.TotalValue, summary.FeeTotal);
    }

    public async Task<BatchResult<BatchAcceptedDto>> SubmitXmlAsync(BatchCaller caller, byte[] xml, byte[] payloadHash, CancellationToken cancellationToken = default)
    {
        var (batch, errors) = LegacyBatchXml.Parse(xml);
        if (batch is not null)
            return await SubmitAsync(caller, batch, BatchUploadFormat.Xml, payloadHash, cancellationToken);
        var error = BatchError.Invalid(errors!);
        await LogRejectedAsync(caller, null, null, BatchUploadFormat.Xml, payloadHash, error, cancellationToken);
        return error;
    }

    /// <summary>Logs an upload that could not be read (e.g. malformed JSON) as a rejected attempt.</summary>
    public async Task RecordUnreadableAsync(BatchCaller caller, BatchUploadFormat format, byte[] payloadHash, IReadOnlyDictionary<string, string[]> errors,
        CancellationToken cancellationToken = default)
        => await LogRejectedAsync(caller, null, null, format, payloadHash, BatchError.Invalid(errors), cancellationToken);

    /// <summary>Runs every check without storing anything (not logged).</summary>
    public async Task<BatchResult<BatchValidationDto>> ValidateAsync(BatchCaller caller, BatchSubmission batch, CancellationToken cancellationToken = default)
    {
        var (check, error) = await CheckAsync(caller, batch, null, cancellationToken);
        if (error is not null)
            return error;
        return new BatchValidationDto(true, check!.ServiceType.ToString(), batch.Payments.Count, check.PaymentTotal, check.FeeTotal);
    }

    public async Task<BatchResult<BatchValidationDto>> ValidateXmlAsync(BatchCaller caller, byte[] xml, CancellationToken cancellationToken = default)
    {
        var (batch, errors) = LegacyBatchXml.Parse(xml);
        return batch is null ? BatchError.Invalid(errors!) : await ValidateAsync(caller, batch, cancellationToken);
    }

    private sealed record BatchCheck(int SecondaryClientId, string ClientName, ServiceTypeCode ServiceType, decimal PaymentTotal, decimal FeeTotal);

    private async Task<(BatchCheck? Check, BatchError? Error)> CheckAsync(BatchCaller caller, BatchSubmission batch, byte[]? payloadHash,
        CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        BatchRules.CheckFields(batch, _options, errors);
        if (errors.Any)
            return (null, BatchError.Invalid(errors.ToDictionary()));

        var header = batch.Header;
        var client = await db.SecondaryClients.AsNoTracking()
            .Where(s => s.UniqueClientId == header.UniqueClientId)
            .Select(s => new { s.Id, s.Name, s.PrimaryClientId, s.IsActive, s.RequiresReview, PrimaryIsActive = s.PrimaryClient.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        var allowed = client is not null && (caller.IsPrimary ? client.PrimaryClientId == caller.ClientId : client.Id == caller.ClientId);
        if (!allowed)
            return (null, BatchError.Invalid("header.uniqueClientId", "Does not match the client of the API key."));
        if (!client!.IsActive || !client.PrimaryIsActive || client.RequiresReview)
            return (null, BatchError.Invalid("header.uniqueClientId", "The client is not active."));

        if (await FindDuplicateAsync(client.Id, header, payloadHash, cancellationToken) is { } duplicate)
            return (new BatchCheck(client.Id, client.Name, default, 0, 0), duplicate);

        var serviceType = BatchRules.ParseServiceType(header.ServiceType)!.Value;
        var paymentTotal = batch.Payments.Sum(p => p.Amount);
        var feeTotal = 0m;
        if (batch.Footer.Records != batch.Payments.Count)
            errors.Add("footer.records", $"Footer records ({batch.Footer.Records}) does not match the number of payments ({batch.Payments.Count}).");
        if (batch.Footer.TotalValue != paymentTotal)
        {
            var rate = await db.BillingRates.AsNoTracking()
                .Where(r => r.ServiceTypeId == (byte)serviceType && r.EffectiveFrom <= header.PaymentDate)
                .OrderByDescending(r => r.EffectiveFrom)
                .Select(r => (decimal?)r.FeePerRecord)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;
            feeTotal = rate * batch.Payments.Count;
            if (rate == 0 || batch.Footer.TotalValue != paymentTotal + feeTotal)
            {
                errors.Add("footer.totalValue", rate == 0
                    ? $"Footer total ({batch.Footer.TotalValue:0.00}) does not match the sum of the payments ({paymentTotal:0.00})."
                    : $"Footer total ({batch.Footer.TotalValue:0.00}) must equal the sum of the payments ({paymentTotal:0.00}) or the sum plus fees ({paymentTotal + feeTotal:0.00}).");
                feeTotal = 0;
            }
        }

        var today = Today();
        var lastDay = header.PaymentDate > today.AddDays(14) ? header.PaymentDate : today.AddDays(14);
        var holidays = (await db.PublicHolidays.AsNoTracking()
            .Where(h => h.HolidayDate >= today && h.HolidayDate <= lastDay)
            .Select(h => h.HolidayDate)
            .ToListAsync(cancellationToken)).ToHashSet();
        if (BatchRules.CheckPaymentDate(serviceType, header.PaymentDate, today, holidays) is { } dateError)
            errors.Add("header.paymentDate", dateError);

        var check = new BatchCheck(client.Id, client.Name, serviceType, paymentTotal, feeTotal);
        return errors.Any ? (check, BatchError.Invalid(errors.ToDictionary())) : (check, null);
    }

    /// <summary>
    /// A reused SequenceNumber for the same client is a conflict. UniquePaymentReference is not checked: legacy clients send their
    /// client ID there on every batch. When the client retries the identical payload the conflict carries the existing batch id
    /// so the client can treat it as success.
    /// </summary>
    private async Task<BatchError?> FindDuplicateAsync(int secondaryClientId, BatchHeader header, byte[]? payloadHash, CancellationToken cancellationToken)
    {
        var existingId = await db.PaymentSummaries.AsNoTracking()
            .Where(s => s.SecondaryClientId == secondaryClientId && s.SequenceNumber == header.SequenceNumber)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingId is not { } id)
            return null;

        var samePayload = payloadHash is not null && await db.BatchUploads
            .AnyAsync(u => u.PaymentSummaryId == id && u.IsAccepted && u.PayloadHash == payloadHash, cancellationToken);
        return samePayload
            ? BatchError.Conflict($"This batch was already accepted as batch {id}.", id)
            : BatchError.Conflict($"SequenceNumber has already been used by batch {id}.", id);
    }

    private static PaymentSummary BuildSummary(BatchSubmission batch, BatchCheck check)
    {
        var h = batch.Header;
        var summary = new PaymentSummary
        {
            SecondaryClientId = check.SecondaryClientId,
            ServiceTypeId = (byte)check.ServiceType,
            PaymentDate = h.PaymentDate,
            RejectionAccountName = h.RejectionAccountName,
            RejectionBranchCode = h.RejectionBranchCode,
            RejectionAccountNumber = h.RejectionAccountNumber,
            SequenceNumber = h.SequenceNumber,
            UniqueReference = Guid.NewGuid().ToString("D").ToUpperInvariant(),
            ClientReference = h.UniquePaymentReference,
            TotalRecords = batch.Payments.Count,
            TotalValue = check.PaymentTotal,
            FeeTotal = check.FeeTotal,
            ReconStatusId = (byte)ReconStatusCode.Unreconciled
        };
        foreach (var p in batch.Payments)
        {
            var record = new PaymentRecord
            {
                EmployeeCode = p.EmployeeCode,
                Surname = p.Surname,
                FirstName = p.FirstName,
                Initials = p.Initials,
                IdOrPassport = p.IdOrPassport,
                Cell = p.Cell,
                Email = p.Email,
                BranchCode = p.BranchCode,
                AccountNumber = p.AccountNumber,
                AccountType = p.AccountType,
                Amount = p.Amount,
                BankReference = p.BankReference,
                TransactionStatusId = (byte)TransactionStatusCode.Pending
            };
            record.StatusHistory.Add(new PaymentRecordStatusHistory { TransactionStatusId = (byte)TransactionStatusCode.Pending, Reason = "Uploaded" });
            summary.PaymentRecords.Add(record);
        }
        AddContacts(summary, ContactPurpose.UploadConfirmation, batch.Notifications?.UploadConfirmations);
        AddContacts(summary, ContactPurpose.RejectionConfirmation, batch.Notifications?.RejectionConfirmations);
        AddContacts(summary, ContactPurpose.FundingConfirmation, batch.Notifications?.FundingConfirmations);
        return summary;
    }

    private static void AddContacts(PaymentSummary summary, ContactPurpose purpose, IReadOnlyList<BatchContactInfo>? contacts)
    {
        foreach (var c in contacts ?? [])
            summary.Contacts.Add(new BatchContact
            {
                Purpose = purpose,
                Name = c.Name,
                Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email,
                CellNumber = string.IsNullOrWhiteSpace(c.CellNumber) ? null : c.CellNumber
            });
    }

    private static BatchUpload NewUpload(BatchCaller caller, int? secondaryClientId, BatchSubmission? batch, BatchUploadFormat format, byte[] payloadHash,
        PaymentSummary? summary, BatchError? error = null) => new()
    {
        SecondaryClientId = secondaryClientId,
        ClientApiKeyId = caller.ApiKeyId,
        UniqueClientId = caller.UniqueClientId,
        UniqueReference = Truncate(batch?.Header?.UniquePaymentReference, 100),
        Format = format,
        IsAccepted = summary is not null,
        RecordCount = batch?.Payments?.Count,
        TotalValue = batch?.Footer?.TotalValue,
        ErrorsJson = error is null ? null : JsonSerializer.Serialize(error.Errors ?? new Dictionary<string, string[]> { [""] = [error.Message] }),
        PayloadHash = payloadHash,
        PaymentSummary = summary
    };

    private async Task LogRejectedAsync(BatchCaller caller, int? secondaryClientId, BatchSubmission? batch, BatchUploadFormat format, byte[] payloadHash,
        BatchError error, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        db.BatchUploads.Add(NewUpload(caller, secondaryClientId, batch, format, payloadHash, null, error));
        await db.SaveChangesAsync(cancellationToken);
    }

    private DateOnly Today()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }

    private static string? Truncate(string? value, int length) => value is null || value.Length <= length ? value : value[..length];
}
