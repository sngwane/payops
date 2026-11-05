using Microsoft.EntityFrameworkCore;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Client.Data.Services;

/// <summary>A primary client's own bank accounts: at most one active account per account type.</summary>
public class ClientBankAccountService(ClientDbContext db, TimeProvider clock)
{
    private const string DuplicateTypeMessage = "The primary client already has an active account of this type. Deactivate it first.";

    public async Task<ClientResult<IReadOnlyList<ClientBankAccountDto>>> ListAsync(int primaryClientId, CancellationToken cancellationToken = default)
    {
        if (!await db.PrimaryClients.AnyAsync(p => p.Id == primaryClientId, cancellationToken))
            return PrimaryNotFound(primaryClientId);
        var items = await Project(db.ClientBankAccounts.AsNoTracking()
                .Where(a => a.PrimaryClientId == primaryClientId)
                .OrderByDescending(a => a.IsActive).ThenBy(a => a.AccountTypeId).ThenBy(a => a.Id))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<ClientResult<ClientBankAccountDto>> GetAsync(int id, CancellationToken cancellationToken = default)
        => await Project(db.ClientBankAccounts.AsNoTracking().Where(a => a.Id == id)).SingleOrDefaultAsync(cancellationToken) is { } dto
            ? dto
            : NotFound(id);

    public async Task<ClientResult<ClientBankAccountDto>> CreateAsync(int primaryClientId, CreateBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        var v = new Validation();
        if (!Enum.IsDefined((AccountTypeCode)request.AccountTypeId))
            v.Add("accountTypeId", "Must be 1 (Nominated) or 2 (Charges).");
        var accountName = v.Required("accountName", request.AccountName, Validation.NameMaxLength);
        var branchCode = v.Digits("branchCode", request.BranchCode, Validation.BranchCodeMaxLength);
        var accountNumber = v.Digits("accountNumber", request.AccountNumber, Validation.AccountNumberMaxLength);
        var glAccountNumber = v.Optional("glAccountNumber", request.GlAccountNumber, Validation.AccountNumberMaxLength);
        if (!v.IsValid)
            return v.ToError();

        if (!await db.PrimaryClients.AnyAsync(p => p.Id == primaryClientId, cancellationToken))
            return PrimaryNotFound(primaryClientId);
        if (await db.ClientBankAccounts.AnyAsync(a => a.PrimaryClientId == primaryClientId && a.AccountTypeId == request.AccountTypeId && a.IsActive, cancellationToken))
            return ClientError.Conflict(DuplicateTypeMessage);

        var account = new ClientBankAccount
        {
            PrimaryClientId = primaryClientId,
            AccountTypeId = request.AccountTypeId,
            AccountName = accountName,
            BranchCode = branchCode,
            AccountNumber = accountNumber,
            GlAccountNumber = glAccountNumber
        };
        db.ClientBankAccounts.Add(account);
        if (await DbErrors.SaveAsync(db, cancellationToken, DuplicateTypeMessage) is { } saveError)
            return saveError;
        return await GetAsync(account.Id, cancellationToken);
    }

    public async Task<ClientResult<ClientBankAccountDto>> UpdateAsync(int id, UpdateBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        var v = new Validation();
        var accountName = v.Required("accountName", request.AccountName, Validation.NameMaxLength);
        var branchCode = v.Digits("branchCode", request.BranchCode, Validation.BranchCodeMaxLength);
        var accountNumber = v.Digits("accountNumber", request.AccountNumber, Validation.AccountNumberMaxLength);
        var glAccountNumber = v.Optional("glAccountNumber", request.GlAccountNumber, Validation.AccountNumberMaxLength);
        var rowVersion = v.RowVersion("rowVersion", request.RowVersion);
        if (!v.IsValid)
            return v.ToError();

        var account = await db.ClientBankAccounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
            return NotFound(id);
        if (!account.IsActive)
            return ClientError.Conflict("Inactive bank accounts can't be changed.");

        db.Entry(account).Property(a => a.RowVersion).OriginalValue = rowVersion;
        account.AccountName = accountName;
        account.BranchCode = branchCode;
        account.AccountNumber = accountNumber;
        account.GlAccountNumber = glAccountNumber;
        account.ModifiedDate = clock.GetUtcNow().UtcDateTime;
        if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
            return saveError;
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ClientResult<ClientBankAccountDto>> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var account = await db.ClientBankAccounts.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null)
            return NotFound(id);
        if (account.IsActive)
        {
            account.IsActive = false;
            account.ModifiedDate = clock.GetUtcNow().UtcDateTime;
            if (await DbErrors.SaveAsync(db, cancellationToken) is { } saveError)
                return saveError;
        }
        return await GetAsync(id, cancellationToken);
    }

    private static ClientError NotFound(int id) => ClientError.NotFound($"Bank account {id} was not found.");

    private static ClientError PrimaryNotFound(int id) => ClientError.NotFound($"Primary client {id} was not found.");

    private static IQueryable<ClientBankAccountDto> Project(IQueryable<ClientBankAccount> accounts)
        => accounts.Select(a => new ClientBankAccountDto(
            a.Id, a.PrimaryClientId, a.AccountTypeId, a.AccountType.Name, a.AccountName, a.BranchCode, a.AccountNumber,
            a.GlAccountNumber, a.IsActive, a.CreatedDate, a.ModifiedDate, DbErrors.ToRowVersion(a.RowVersion)));
}
