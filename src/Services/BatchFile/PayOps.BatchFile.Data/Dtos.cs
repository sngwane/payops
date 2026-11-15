using PayOps.Database.Lookups;

namespace PayOps.BatchFile.Data;

/// <summary>A payment batch as submitted by a client (JSON body, or converted from the legacy XML).</summary>
public sealed record BatchSubmission(BatchHeader Header, IReadOnlyList<BatchPaymentLine> Payments, BatchFooter Footer, BatchNotifications? Notifications = null);

public sealed record BatchHeader(
    string UniqueClientId, DateOnly PaymentDate, string RejectionAccountName, string RejectionBranchCode, string RejectionAccountNumber,
    string UniquePaymentReference, string ServiceType, long SequenceNumber);

public sealed record BatchPaymentLine(
    string EmployeeCode, string Surname, string? FirstName, string? Initials, string? IdOrPassport, string? Cell, string? Email,
    string BranchCode, string AccountNumber, string AccountType, decimal Amount, string BankReference);

public sealed record BatchFooter(DateOnly CurrentDate, int Records, decimal TotalValue);

/// <summary>Contacts for this batch; when given they override the client's contacts.</summary>
public sealed record BatchNotifications(
    IReadOnlyList<BatchContactInfo>? UploadConfirmations = null,
    IReadOnlyList<BatchContactInfo>? RejectionConfirmations = null,
    IReadOnlyList<BatchContactInfo>? FundingConfirmations = null);

public sealed record BatchContactInfo(string Name, string? Email, string? CellNumber);

/// <summary>The client calling with an API key; a primary client may submit for any of its secondary clients.</summary>
public sealed record BatchCaller(bool IsPrimary, int ClientId, string UniqueClientId, int ApiKeyId);

public sealed record BatchAcceptedDto(int BatchId, string UniqueReference, string ClientReference, string UniqueClientId, DateOnly PaymentDate, string ServiceType,
    int Records, decimal TotalValue, decimal FeeTotal);

public sealed record BatchValidationDto(bool IsValid, string ServiceType, int Records, decimal TotalValue, decimal FeeTotal);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record BatchListQuery(DateOnly? From = null, DateOnly? To = null, string? UniqueReference = null, string? ClientReference = null, int Page = 1, int PageSize = BatchListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

public sealed record BatchSummaryDto(int Id, string UniqueReference, string ClientReference, string UniqueClientId, string ServiceType, DateOnly PaymentDate,
    long SequenceNumber, int Records, decimal TotalValue, decimal FeeTotal, string ReconStatus, DateTime CreatedDate);

public sealed record TransactionStatusCount(string Status, int Count, decimal Value);

public sealed record BatchPaymentDto(long Id, string EmployeeCode, string Surname, string? FirstName, string BranchCode, string AccountNumber,
    string AccountType, decimal Amount, string? BankReference, string Status, string? RejectionReason);

public sealed record BatchDetailDto(BatchSummaryDto Batch, string? RejectionAccountName, string? RejectionBranchCode, string? RejectionAccountNumber,
    IReadOnlyList<TransactionStatusCount> Statuses, PagedResult<BatchPaymentDto> Payments);

public sealed record RejectedPaymentDto(long PaymentId, string EmployeeCode, string Surname, string BranchCode, string AccountNumber, decimal Amount,
    string? BankReference, string? RejectionReason, string? RefundStatus, DateTime? RejectedDate);

public sealed record UploadListQuery(string? UniqueClientId = null, bool? IsAccepted = null, DateTime? From = null, DateTime? To = null,
    int Page = 1, int PageSize = BatchListQuery.DefaultPageSize);

public sealed record BatchUploadDto(long Id, string UniqueClientId, int? SecondaryClientId, string? UniqueReference, string Format, bool IsAccepted,
    int? RecordCount, decimal? TotalValue, IReadOnlyDictionary<string, string[]>? Errors, int? BatchId, DateTime ReceivedDate);

public sealed record PublicHolidayDto(int Id, DateOnly HolidayDate, string Name);

public sealed record PublicHolidayRequest(DateOnly HolidayDate, string Name);

public sealed record BillingRateDto(int Id, string ServiceType, decimal FeePerRecord, DateOnly EffectiveFrom);

public sealed record CreateBillingRateRequest(ServiceTypeCode ServiceType, decimal FeePerRecord, DateOnly EffectiveFrom);

public sealed record UpdateBillingRateRequest(decimal FeePerRecord, DateOnly EffectiveFrom);
