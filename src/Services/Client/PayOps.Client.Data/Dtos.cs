namespace PayOps.Client.Data;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ClientListQuery(string? Search = null, bool? IsActive = null, int Page = 1, int PageSize = ClientListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

public sealed record SecondaryClientListQuery(
    string? Search = null, bool? IsActive = null, bool? RequiresReview = null, int? PrimaryClientId = null,
    int Page = 1, int PageSize = ClientListQuery.DefaultPageSize);

public sealed record PrimaryClientDto(
    int Id, string UniqueClientId, string Name, bool AutoProcess, string? LogoFileName, bool IsActive,
    int SecondaryClientCount, DateTime CreatedDate, DateTime? ModifiedDate, string RowVersion);

public sealed record SecondaryClientDto(
    int Id, int PrimaryClientId, string PrimaryClientUniqueId, string PrimaryClientName, string UniqueClientId, string Name,
    bool IsActive, bool RequiresReview, DateTime CreatedDate, DateTime? ModifiedDate, string RowVersion);

public sealed record ClientBankAccountDto(
    int Id, int PrimaryClientId, byte AccountTypeId, string AccountType, string AccountName, string BranchCode,
    string AccountNumber, string? GlAccountNumber, bool IsActive, DateTime CreatedDate, DateTime? ModifiedDate, string RowVersion);

public sealed record ClientApiKeyDto(
    int Id, int? PrimaryClientId, int? SecondaryClientId, string Name, string KeyPrefix, DateTime? ExpiryDate,
    DateTime? RevokedDate, DateTime? LastUsedDate, DateTime CreatedDate);

/// <summary>Returned once, when a key is created. <see cref="Key"/> is never stored and can't be retrieved again.</summary>
public sealed record CreatedClientApiKeyDto(ClientApiKeyDto ApiKey, string Key);

public enum ClientKind { Primary, Secondary }

/// <summary>The client an API key belongs to.</summary>
public sealed record ApiKeyOwner(ClientKind Kind, int ClientId, string UniqueClientId, int ApiKeyId);

public sealed record CreatePrimaryClientRequest(string UniqueClientId, string Name, bool AutoProcess = false, string? LogoFileName = null);

public sealed record UpdatePrimaryClientRequest(string Name, bool AutoProcess, string? LogoFileName, string RowVersion);

public sealed record CreateSecondaryClientRequest(string UniqueClientId, string Name);

public sealed record UpdateSecondaryClientRequest(string Name, string RowVersion);

public sealed record ReassignSecondaryClientRequest(int PrimaryClientId, string RowVersion);

public sealed record CreateBankAccountRequest(byte AccountTypeId, string AccountName, string BranchCode, string AccountNumber, string? GlAccountNumber = null);

public sealed record UpdateBankAccountRequest(string AccountName, string BranchCode, string AccountNumber, string? GlAccountNumber, string RowVersion);

public sealed record CreateApiKeyRequest(string Name, DateTime? ExpiryDate = null);
