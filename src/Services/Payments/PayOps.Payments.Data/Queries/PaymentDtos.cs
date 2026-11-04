namespace PayOps.Payments.Data.Queries;

public sealed record ClientBatchDto(
    int Id,
    string UniqueReference,
    string ClientUniqueId,
    string ClientName,
    string ServiceType,
    DateOnly PaymentDate,
    long SequenceNumber,
    int TotalRecords,
    decimal TotalValue,
    string ReconStatus,
    string? BankBatchReference,
    IReadOnlyList<PaymentRecordDto> Records);

public sealed record PaymentRecordDto(
    long Id,
    string EmployeeCode,
    string Surname,
    string? FirstName,
    string BranchCode,
    string AccountNumber,
    string AccountType,
    decimal Amount,
    string? BankReference,
    string TransactionStatus,
    string? RejectionReason);

public sealed record PaidPaymentDto(
    long PaymentRecordId,
    string ClientUniqueId,
    string ClientName,
    string UniqueReference,
    string EmployeeCode,
    string Surname,
    decimal Amount,
    DateTime PaidDateUtc);

public sealed record RejectionDto(
    long Id,
    string ClientUniqueId,
    string ClientName,
    string? PaymentBatchReference,
    string? PaymentFileName,
    string? RefundBatchReference,
    string? RefundFileName,
    string ServiceType,
    DateOnly PaymentDate,
    string AccountNumber,
    string BranchCode,
    decimal Amount,
    string? PaymentReason,
    string? RefundReason);
