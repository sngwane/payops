namespace PayOps.Payments.Data;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>Time zone that defines "today" for Paid Away and the day boundaries of Paid History.</summary>
    public string TimeZone { get; set; } = "Africa/Johannesburg";

    /// <summary>Longest date range one Paid History request may cover.</summary>
    public int MaxHistoryDays { get; set; } = 31;
}
