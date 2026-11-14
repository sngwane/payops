using PayOps.Database.Lookups;

namespace PayOps.Database.Entities;

/// <summary>Log of every authenticated batch upload attempt, accepted or rejected. The payload itself is not stored.</summary>
public class BatchUpload
{
    public long Id { get; set; }
    public int? SecondaryClientId { get; set; }
    public int? ClientApiKeyId { get; set; }
    public string UniqueClientId { get; set; } = string.Empty;
    public string? UniqueReference { get; set; }
    public BatchUploadFormat Format { get; set; }
    public bool IsAccepted { get; set; }
    public int? RecordCount { get; set; }
    public decimal? TotalValue { get; set; }

    /// <summary>Validation errors as JSON ({ "field": ["message"] }) when rejected.</summary>
    public string? ErrorsJson { get; set; }

    /// <summary>SHA-256 of the raw request body, used to recognise retries of the same upload.</summary>
    public byte[] PayloadHash { get; set; } = [];
    public int? PaymentSummaryId { get; set; }
    public DateTime ReceivedDate { get; set; }

    public SecondaryClient? SecondaryClient { get; set; }
    public ClientApiKey? ClientApiKey { get; set; }
    public PaymentSummary? PaymentSummary { get; set; }
}
