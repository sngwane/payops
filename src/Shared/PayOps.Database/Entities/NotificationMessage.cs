using PayOps.Database.Lookups;

namespace PayOps.Database.Entities;

/// <summary>
/// Transactional outbox row: written in the same transaction as the business change, sent later by the Notifications worker.
/// </summary>
public class NotificationMessage
{
    public long Id { get; set; }
    public NotificationType Type { get; set; }
    public NotificationMessageStatus Status { get; set; } = NotificationMessageStatus.Pending;

    /// <summary>Unique per logical message so a retried business operation never queues it twice.</summary>
    public string DeduplicationKey { get; set; } = string.Empty;
    public int? SecondaryClientId { get; set; }
    public int? PaymentSummaryId { get; set; }

    /// <summary>Template placeholders as JSON.</summary>
    public string PayloadJson { get; set; } = "{}";
    public int Attempts { get; set; }
    public DateTime? NextAttemptDate { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? SentDate { get; set; }

    public SecondaryClient? SecondaryClient { get; set; }
    public PaymentSummary? PaymentSummary { get; set; }
}
