namespace PayOps.LegacyImport.Legacy;

// Only the legacy BulkPayments columns the import reads. Names match the legacy schema.

public sealed class LegacyPrimaryClient
{
    public int PrimaryClientId { get; set; }
    public string? ClientName { get; set; }
    public string? UniqueClientId { get; set; }
    public bool? AutoProcess { get; set; }
    public string? AccessKey { get; set; }
    public string? LogoFilename { get; set; }
}

public sealed class LegacySecondaryClient
{
    public int SecondaryClientId { get; set; }
    public string? ClientName { get; set; }
    public string? UniqueClientId { get; set; }
    public int? PrimaryClientId { get; set; }
    public string? AccessKey { get; set; }
}

public sealed class LegacyBankAccount
{
    public int BankAccountId { get; set; }
    public string? AccountNo { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? GLAccNo { get; set; }
}

public sealed class LegacyPrimaryClientBankAccountType
{
    public int? PrimaryClientId { get; set; }
    public int? BankAccountId { get; set; }
    public int? AccountTypeId { get; set; }
}

public sealed class LegacyBank
{
    public int BankId { get; set; }
    public string? Name { get; set; }
}

public sealed class LegacyBankClientProfile
{
    public int BankClientProfileId { get; set; }
    public int BankID { get; set; }
    public string ClientProfileNumber { get; set; } = string.Empty;
    public string ClientIdentifier { get; set; } = string.Empty;
    public long RunningSequence { get; set; }
    public int DailySequence { get; set; }
    public DateTime? LastUpdateDate { get; set; }
    public bool? Active { get; set; }
}

public sealed class LegacyBillingRate
{
    public string? DebtorsCode { get; set; }
    public int? ServiceTypeId { get; set; }
    public decimal? Rate { get; set; }
    public DateTime DateEffective { get; set; }
}

public sealed class LegacyPublicHoliday
{
    public int PublicHolidayId { get; set; }
    public DateOnly Date { get; set; }
}

public sealed class LegacyBankRun
{
    public int BankRunId { get; set; }
    public string? BankFileName { get; set; }
    public string? FileSequenceNumber { get; set; }
    public DateTime? DateTrn { get; set; }
    public int? StatusId { get; set; }
    public string? BankRunReason { get; set; }
}

public sealed class LegacyHeader
{
    public int HeaderId { get; set; }
    public string? BatchId { get; set; }
    public int? BankRunId { get; set; }
    public string? UniqueClientId { get; set; }
    public int? ServiceTypeId { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? RejectionAccountName { get; set; }
    public string? RejectionBranchCode { get; set; }
    public string? RejectionAccountNumber { get; set; }
    public string? UniquePaymentReference { get; set; }
    public int? ReconStatusId { get; set; }
    public DateTime? DateTrn { get; set; }
    public long? SequenceNumber { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int? Records { get; set; }
    public decimal? TotalValue { get; set; }
}

public sealed class LegacyTransaction
{
    public int TransactionId { get; set; }
    public string? BatchId { get; set; }
    public string? BankFileReference { get; set; }
    public string? EmployeeCode { get; set; }
    public string? EmployeeSurname { get; set; }
    public string? EmployeeFirstname { get; set; }
    public string? EmployeeInitials { get; set; }
    public string? EmployeeIdOrPassport { get; set; }
    public string? EmployeeCell { get; set; }
    public string? EmployeeEmail { get; set; }
    public string? BranchCode { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountType { get; set; }
    public decimal? Amount { get; set; }
    public string? BankReference { get; set; }
    public DateTime? DateTrn { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int? TransactionStatusId { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class LegacyRejection
{
    public int RejectionId { get; set; }
    public int? BankRunId { get; set; }
    public string? BankReference { get; set; }
    public string? BankFileReference { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? BranchCode { get; set; }
    public decimal? Amount { get; set; }
    public int? ServiceTypeId { get; set; }
    public int? TransactionStatusId { get; set; }
    public int? TransactionId { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class LegacyHeaderComment
{
    public int CommentId { get; set; }
    public int HeaderId { get; set; }
    public string? Comment { get; set; }
    public DateTime CommentDate { get; set; }
}
