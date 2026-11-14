using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Data.Services;

/// <summary>Public holidays and billing rates used by the batch checks.</summary>
public sealed class ReferenceDataService(BatchFileDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<PublicHolidayDto>> ListHolidaysAsync(int? year, CancellationToken cancellationToken = default)
    {
        var holidays = db.PublicHolidays.AsNoTracking();
        if (year is { } y)
            holidays = holidays.Where(h => h.HolidayDate >= new DateOnly(y, 1, 1) && h.HolidayDate <= new DateOnly(y, 12, 31));
        return await holidays.OrderBy(h => h.HolidayDate).Select(h => new PublicHolidayDto(h.Id, h.HolidayDate, h.Name)).ToListAsync(cancellationToken);
    }

    public async Task<BatchResult<PublicHolidayDto>> CreateHolidayAsync(PublicHolidayRequest request, CancellationToken cancellationToken = default)
    {
        if (ValidateHoliday(request) is { } invalid)
            return invalid;
        var holiday = new PublicHoliday { HolidayDate = request.HolidayDate, Name = request.Name.Trim() };
        db.PublicHolidays.Add(holiday);
        return await SaveAsync(() => new PublicHolidayDto(holiday.Id, holiday.HolidayDate, holiday.Name),
            $"A public holiday on {request.HolidayDate:yyyy-MM-dd} already exists.", cancellationToken);
    }

    public async Task<BatchResult<PublicHolidayDto>> UpdateHolidayAsync(int id, PublicHolidayRequest request, CancellationToken cancellationToken = default)
    {
        if (ValidateHoliday(request) is { } invalid)
            return invalid;
        var holiday = await db.PublicHolidays.SingleOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (holiday is null)
            return BatchError.NotFound($"Public holiday {id} was not found.");
        holiday.HolidayDate = request.HolidayDate;
        holiday.Name = request.Name.Trim();
        holiday.ModifiedDate = clock.GetUtcNow().UtcDateTime;
        return await SaveAsync(() => new PublicHolidayDto(holiday.Id, holiday.HolidayDate, holiday.Name),
            $"A public holiday on {request.HolidayDate:yyyy-MM-dd} already exists.", cancellationToken);
    }

    public async Task<IReadOnlyList<BillingRateDto>> ListBillingRatesAsync(ServiceTypeCode? serviceType, CancellationToken cancellationToken = default)
    {
        var rates = db.BillingRates.AsNoTracking();
        if (serviceType is { } type)
            rates = rates.Where(r => r.ServiceTypeId == (byte)type);
        return await rates.OrderBy(r => r.ServiceTypeId).ThenByDescending(r => r.EffectiveFrom)
            .Select(r => new BillingRateDto(r.Id, r.ServiceType.Name, r.FeePerRecord, r.EffectiveFrom))
            .ToListAsync(cancellationToken);
    }

    public async Task<BatchResult<BillingRateDto>> CreateBillingRateAsync(CreateBillingRateRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        if (!Enum.IsDefined(request.ServiceType))
            errors.Add("serviceType", "Unknown service type.");
        CheckFee(request.FeePerRecord, errors);
        if (errors.Any)
            return BatchError.Invalid(errors.ToDictionary());
        var rate = new BillingRate { ServiceTypeId = (byte)request.ServiceType, FeePerRecord = request.FeePerRecord, EffectiveFrom = request.EffectiveFrom };
        db.BillingRates.Add(rate);
        return await SaveAsync(() => new BillingRateDto(rate.Id, request.ServiceType.ToString(), rate.FeePerRecord, rate.EffectiveFrom),
            $"A {request.ServiceType} rate effective {request.EffectiveFrom:yyyy-MM-dd} already exists.", cancellationToken);
    }

    public async Task<BatchResult<BillingRateDto>> UpdateBillingRateAsync(int id, UpdateBillingRateRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new ValidationErrors();
        CheckFee(request.FeePerRecord, errors);
        if (errors.Any)
            return BatchError.Invalid(errors.ToDictionary());
        var rate = await db.BillingRates.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rate is null)
            return BatchError.NotFound($"Billing rate {id} was not found.");
        rate.FeePerRecord = request.FeePerRecord;
        rate.EffectiveFrom = request.EffectiveFrom;
        rate.ModifiedDate = clock.GetUtcNow().UtcDateTime;
        return await SaveAsync(() => new BillingRateDto(rate.Id, ((ServiceTypeCode)rate.ServiceTypeId).ToString(), rate.FeePerRecord, rate.EffectiveFrom),
            $"A rate effective {request.EffectiveFrom:yyyy-MM-dd} already exists for this service type.", cancellationToken);
    }

    private static BatchError? ValidateHoliday(PublicHolidayRequest request)
        => string.IsNullOrWhiteSpace(request.Name) ? BatchError.Invalid("name", "Value is required.")
            : request.Name.Trim().Length > 100 ? BatchError.Invalid("name", "Must be 100 characters or fewer.")
            : null;

    private static void CheckFee(decimal fee, ValidationErrors errors)
    {
        if (fee < 0)
            errors.Add("feePerRecord", "May not be negative.");
        else if (decimal.Round(fee, 2) != fee)
            errors.Add("feePerRecord", "May have at most 2 decimal places.");
    }

    private async Task<BatchResult<T>> SaveAsync<T>(Func<T> result, string duplicateMessage, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return result();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            return BatchError.Conflict(duplicateMessage);
        }
    }
}
