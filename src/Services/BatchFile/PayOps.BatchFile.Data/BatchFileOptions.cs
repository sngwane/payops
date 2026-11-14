using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Data;

public sealed class BatchFileOptions
{
    public const string SectionName = "BatchFile";

    public int MaxRecords { get; set; } = 50_000;
    public long MaxRequestBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Time zone that defines "today" for payment-date rules.</summary>
    public string TimeZone { get; set; } = "Africa/Johannesburg";

    /// <summary>Service types clients may upload. Legacy accepted SDV and OneDay; TwoDay was added in PayOps.</summary>
    public ServiceTypeCode[]? AllowedServiceTypes { get; set; }

    public IReadOnlyCollection<ServiceTypeCode> EffectiveServiceTypes
        => AllowedServiceTypes is { Length: > 0 } ? AllowedServiceTypes : [ServiceTypeCode.SDV, ServiceTypeCode.OneDay, ServiceTypeCode.TwoDay];
}
