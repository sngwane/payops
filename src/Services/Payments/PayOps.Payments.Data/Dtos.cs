using PayOps.Database.Lookups;

namespace PayOps.Payments.Data;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record Paging(int Page = 1, int PageSize = Paging.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

/// <summary>The signed-in staff member, taken from the Entra token (or the development user).</summary>
public sealed record StaffIdentity(Guid ExternalId, string Username, string? Email = null, string? FirstName = null, string? LastName = null);

// Batches

public sealed record OpsBatchQuery(
    string? Client = null,
    int? PrimaryClientId = null,
    int? SecondaryClientId = null,
    string? ClientReference = null,
    string? UniqueReference = null,
    ServiceTypeCode? ServiceType = null,
    DateOnly? PaymentDateFrom = null,
    DateOnly? PaymentDateTo = null,
    ReconStatusCode? ReconStatus = null,
    bool? OnHold = null,
    bool? InBankRun = null,
    bool? HasRejections = null);

public sealed record OpsBatchDto(
    int Id,
    string UniqueReference,
    string ClientReference,
    int SecondaryClientId,
    string ClientUniqueId,
    string ClientName,
    string PrimaryClientUniqueId,
    ServiceTypeCode ServiceType,
    DateOnly PaymentDate,
    long SequenceNumber,
    int TotalRecords,
    decimal TotalValue,
    decimal FeeTotal,
    ReconStatusCode ReconStatus,
    bool IsOnHold,
    int? BankRunId,
    string? BankRunReference,
    DateTime CreatedDate);

public sealed record StatusCountDto(TransactionStatusCode Status, int Count, decimal Amount);

public sealed record OpsBatchDetailDto(
    int Id,
    string UniqueReference,
    string ClientReference,
    int? LegacyId,
    int SecondaryClientId,
    string ClientUniqueId,
    string ClientName,
    int PrimaryClientId,
    string PrimaryClientUniqueId,
    string PrimaryClientName,
    ServiceTypeCode ServiceType,
    DateOnly PaymentDate,
    long SequenceNumber,
    int TotalRecords,
    decimal TotalValue,
    decimal FeeTotal,
    string? RejectionAccountName,
    string? RejectionBranchCode,
    string? RejectionAccountNumber,
    ReconStatusCode ReconStatus,
    string? ReconStatusChangedBy,
    DateTime? ReconStatusChangedDate,
    bool IsOnHold,
    string? HoldReason,
    string? HeldBy,
    DateTime? HeldDate,
    int? BankRunId,
    string? BankRunReference,
    string? BankRunFileName,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    int RejectionCount,
    IReadOnlyList<StatusCountDto> PaymentStatuses,
    string RowVersion);

public sealed record OpsPaymentQuery(TransactionStatusCode? Status = null, string? Search = null);

public sealed record OpsPaymentDto(
    long Id,
    string EmployeeCode,
    string Surname,
    string? FirstName,
    string? Initials,
    string? IdOrPassport,
    string BranchCode,
    string AccountNumber,
    string AccountType,
    decimal Amount,
    TransactionStatusCode Status,
    string? BankReference,
    string? BankFileReference,
    string? RejectionReason,
    string? Comment);

public sealed record StatusHistoryDto(TransactionStatusCode Status, DateTime ChangedDate, string? Reason, int? BankRunId, long? RejectionId, string? ChangedBy);

public sealed record PaymentDetailDto(
    long Id,
    int BatchId,
    string BatchUniqueReference,
    string ClientUniqueId,
    string ClientName,
    ServiceTypeCode ServiceType,
    DateOnly PaymentDate,
    string EmployeeCode,
    string Surname,
    string? FirstName,
    string? Initials,
    string? IdOrPassport,
    string? Cell,
    string? Email,
    string BranchCode,
    string AccountNumber,
    string AccountType,
    decimal Amount,
    TransactionStatusCode Status,
    string? BankReference,
    string? BankFileReference,
    string? RejectionReason,
    string? Comment,
    long? RejectionId,
    TransactionStatusCode? RejectionStatus,
    IReadOnlyList<StatusHistoryDto> StatusHistory);

public sealed record CommentDto(int Id, string Comment, string? CreatedBy, DateTime CreatedDate);

public sealed record AddCommentRequest(string? Comment);

/// <summary>One change to a batch: from the temporal history (field changes) or a comment (Field = "Comment").</summary>
public sealed record BatchChangeDto(DateTime ChangedDate, string Field, string? OldValue, string? NewValue, string? ChangedBy);

// Operations

public sealed record SetReconStatusRequest(ReconStatusCode? Status, string? Comment, string? RowVersion = null);

public sealed record HoldRequest(string? Reason, string? RowVersion = null);

public sealed record ReleaseRequest(string? Comment, string? RowVersion = null);

// Paid Away / History

public sealed record PaidQuery(string? Client = null, ServiceTypeCode? ServiceType = null);

public sealed record PaidPaymentListItemDto(
    long PaymentId,
    int BatchId,
    string BatchUniqueReference,
    string ClientUniqueId,
    string ClientName,
    ServiceTypeCode ServiceType,
    string EmployeeCode,
    string Surname,
    string BranchCode,
    string AccountNumber,
    decimal Amount,
    string? BankReference,
    DateTime PaidDate);

public sealed record PaidListDto(IReadOnlyList<PaidPaymentListItemDto> Items, int Page, int PageSize, int TotalCount, decimal TotalAmount, DateOnly From, DateOnly To);

// Rejections

public sealed record RejectionQuery(TransactionStatusCode? Status = null, string? Client = null, DateOnly? From = null, DateOnly? To = null);

public sealed record RejectionListItemDto(
    long Id,
    long PaymentId,
    int BatchId,
    string BatchUniqueReference,
    string ClientUniqueId,
    string ClientName,
    ServiceTypeCode ServiceType,
    DateOnly PaymentDate,
    string? OriginalBankRunReference,
    string? RefundBankRunReference,
    string AccountName,
    string BranchCode,
    string AccountNumber,
    decimal Amount,
    TransactionStatusCode Status,
    string? PaymentRejectionReason,
    string? RefundRejectionReason,
    DateTime CreatedDate,
    string? ConfirmedBy,
    DateTime? ConfirmedDate);

public sealed record RejectionDetailDto(
    long Id,
    TransactionStatusCode Status,
    string AccountName,
    string BranchCode,
    string AccountNumber,
    decimal Amount,
    string? BankReference,
    string? BankFileReference,
    string? RefundRejectionReason,
    int? RefundBankRunId,
    string? RefundBankRunReference,
    DateTime CreatedDate,
    DateTime? ModifiedDate,
    string? ConfirmedBy,
    DateTime? ConfirmedDate,
    string RowVersion,
    PaymentDetailDto Payment);

public sealed record UpdateRefundAccountRequest(string? AccountName, string? BranchCode, string? AccountNumber, string? RowVersion = null);

public sealed record ConfirmRejectionRequest(string? Comment, string? RowVersion = null);
