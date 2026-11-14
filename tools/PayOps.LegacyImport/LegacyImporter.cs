using Microsoft.EntityFrameworkCore;
using PayOps.Client.Data.Services;
using PayOps.Database;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;
using PayOps.LegacyImport.Legacy;

namespace PayOps.LegacyImport;

public sealed record ImportOptions(bool DryRun = false, int ChunkSize = 5000);

/// <summary>
/// Copies legacy BulkPayments data into PayOps. Rows are matched on their legacy key, so re-running updates what is
/// already there instead of duplicating it. A dry run does the whole import inside one transaction and rolls it back.
/// </summary>
public sealed class LegacyImporter(LegacyDbContext legacy, PayOpsDbContext db, ImportOptions options, TextWriter log)
{
    public const string UnmatchedPrimaryId = "LEGACY-UNMATCHED";
    public const string ImportUsername = "legacy.import";
    public const string PlaceholderNamePrefix = "Legacy unmatched: ";
    private const string Missing = "(missing)";
    private static readonly Guid ImportUserExternalId = new("6c3f1d0e-4a5b-4c1d-9e7f-1e9a1e9a0001");
    private static readonly StringComparer Ids = StringComparer.OrdinalIgnoreCase;

    private readonly ImportReport _report = new(options.DryRun);
    private readonly DateTime _now = DateTime.UtcNow;
    private int _importUserId;
    private int? _unmatchedPrimaryId;
    private int _placeholderCounter;
    private Dictionary<int, int> _banks = [];
    private Dictionary<int, int> _primaries = [];
    private Dictionary<int, int> _secondaries = [];
    private Dictionary<int, int> _bankRuns = [];
    private Dictionary<int, int> _summaries = [];
    private readonly Dictionary<string, string> _usedClientIds = new(Ids);
    private readonly Dictionary<string, int> _secondaryByClientId = new(Ids);
    private readonly Dictionary<string, int> _placeholders = new(Ids);
    private List<LegacyHeader> _headers = [];
    private Dictionary<string, (int Count, decimal Amount)> _legacyPaymentsByBatchId = new(Ids);
    private Dictionary<string, int> _headerByBatchId = new(Ids);
    private HashSet<string> _ambiguousBatchIds = new(Ids);

    public async Task<ImportReport> RunAsync(CancellationToken ct = default)
    {
        await using var dryRunTransaction = options.DryRun ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            await ImportUserAsync(ct);
            await PublicHolidaysAsync(ct);
            await BillingRatesAsync(ct);
            await BanksAsync(ct);
            await BankClientProfilesAsync(ct);
            await PrimaryClientsAsync(ct);
            await SecondaryClientsAsync(ct);
            await BankAccountsAsync(ct);
            await ApiKeysAsync(ct);
            await LoadHeadersAsync(ct);
            await BankRunsAsync(ct);
            await PaymentSummariesAsync(ct);
            await PaymentRecordsAsync(ct);
            await RejectionsAsync(ct);
            await CommentsAsync(ct);
            await ReconcileAsync(ct);
        }
        finally
        {
            if (dryRunTransaction is not null) await dryRunTransaction.RollbackAsync(CancellationToken.None);
            _report.FinishedUtc = DateTime.UtcNow;
        }
        return _report;
    }

    private async Task ImportUserAsync(CancellationToken ct)
    {
        var user = await db.AppUsers.SingleOrDefaultAsync(u => u.Username == ImportUsername, ct);
        if (user is null)
        {
            user = new AppUser
            {
                ExternalId = ImportUserExternalId, Username = ImportUsername, Email = "legacy.import@payops.local",
                FirstName = "Legacy", LastName = "Import", IsActive = false, CreatedDate = _now
            };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync(ct);
        }
        _importUserId = user.Id;
    }

    private async Task PublicHolidaysAsync(CancellationToken ct)
    {
        var step = Begin("Public holidays");
        var existing = await db.PublicHolidays.Select(h => h.HolidayDate).ToListAsync(ct);
        var known = existing.ToHashSet();
        foreach (var h in await legacy.PublicHolidays.OrderBy(h => h.PublicHolidayId).ToListAsync(ct))
        {
            step.Read++;
            if (!known.Add(h.Date)) { step.Unchanged++; continue; }
            db.PublicHolidays.Add(new PublicHoliday { HolidayDate = h.Date, Name = "Public holiday", CreatedDate = _now });
            step.Inserted++;
        }
        await SaveAsync(ct);
    }

    private async Task BillingRatesAsync(CancellationToken ct)
    {
        var step = Begin("Billing rates");
        var rates = await legacy.BillingRates.ToListAsync(ct);
        step.Read = rates.Count;
        var debtorsCodes = rates.Select(r => Clean(r.DebtorsCode)).Where(c => c is not null).Distinct(Ids).Count();
        if (debtorsCodes > 1)
            _report.Notes.Add($"Legacy BillingRates has {debtorsCodes} different DebtorsCode values; only the latest rate per service type was imported.");

        foreach (var group in rates.GroupBy(r => r.ServiceTypeId))
        {
            if (!IsServiceType(group.Key))
            {
                foreach (var r in group) _report.Skip(step, "BillingRates", RateKey(r), $"ServiceTypeId {group.Key} is not a known service type");
                continue;
            }
            var ordered = group.OrderByDescending(r => r.DateEffective).ToList();
            var latest = ordered[0];
            foreach (var older in ordered.Skip(1))
                _report.Skip(step, "BillingRates", RateKey(older), "Not the latest rate for this service type");
            if (latest.Rate is not { } fee || fee < 0)
            {
                _report.Skip(step, "BillingRates", RateKey(latest), "Rate is empty or negative");
                continue;
            }
            var serviceType = (byte)group.Key!.Value;
            var from = DateOnly.FromDateTime(latest.DateEffective);
            var e = await db.BillingRates.SingleOrDefaultAsync(b => b.ServiceTypeId == serviceType && b.EffectiveFrom == from, ct);
            var isNew = e is null;
            e ??= new BillingRate { ServiceTypeId = serviceType, EffectiveFrom = from, CreatedDate = _now };
            e.FeePerRecord = Math.Round(fee, 2);
            Track(step, e, isNew);
        }
        await SaveAsync(ct);
    }

    private async Task BanksAsync(CancellationToken ct)
    {
        var step = Begin("Banks");
        var existing = await db.Banks.ToListAsync(ct);
        var byLegacy = existing.Where(b => b.LegacyId != null).ToDictionary(b => b.LegacyId!.Value);
        var codes = existing.Select(b => b.Code).ToHashSet(Ids);
        foreach (var lb in await legacy.Banks.OrderBy(b => b.BankId).ToListAsync(ct))
        {
            step.Read++;
            var name = Required(lb.Name, 100, step, "Bank", lb.BankId, "Name");
            var isNew = !byLegacy.TryGetValue(lb.BankId, out var e);
            if (e is null)
            {
                e = new Bank { LegacyId = lb.BankId, Code = BankCode(name, lb.BankId, codes), IsActive = true };
                codes.Add(e.Code);
            }
            e.Name = name;
            Track(step, e, isNew);
        }
        await SaveAsync(ct);
        _banks = await db.Banks.Where(b => b.LegacyId != null).ToDictionaryAsync(b => b.LegacyId!.Value, b => b.Id, ct);
    }

    private async Task BankClientProfilesAsync(CancellationToken ct)
    {
        var step = Begin("Bank client profiles");
        var existing = await db.BankClientProfiles.ToListAsync(ct);
        var byLegacy = existing.Where(p => p.LegacyId != null).ToDictionary(p => p.LegacyId!.Value);
        var taken = existing.Select(p => (p.BankId, p.ClientProfileNumber.ToUpperInvariant())).ToHashSet();
        foreach (var lp in await legacy.BankClientProfiles.OrderBy(p => p.BankClientProfileId).ToListAsync(ct))
        {
            step.Read++;
            if (!_banks.TryGetValue(lp.BankID, out var bankId))
            {
                _report.Skip(step, "BankClientProfile", lp.BankClientProfileId, $"Bank {lp.BankID} was not imported");
                continue;
            }
            var number = Required(lp.ClientProfileNumber, 20, step, "BankClientProfile", lp.BankClientProfileId, "ClientProfileNumber");
            var isNew = !byLegacy.TryGetValue(lp.BankClientProfileId, out var e);
            if (e is null)
            {
                if (!taken.Add((bankId, number.ToUpperInvariant())))
                {
                    _report.Skip(step, "BankClientProfile", lp.BankClientProfileId, $"Profile number {number} already exists for this bank");
                    continue;
                }
                e = new BankClientProfile { LegacyId = lp.BankClientProfileId, BankId = bankId, ClientProfileNumber = number };
            }
            e.ClientIdentifier = Required(lp.ClientIdentifier, 10, step, "BankClientProfile", lp.BankClientProfileId, "ClientIdentifier");
            e.RunningSequence = lp.RunningSequence;
            e.DailySequence = lp.DailySequence;
            e.LastUpdatedDate = lp.LastUpdateDate;
            e.IsActive = lp.Active ?? true;
            Track(step, e, isNew);
        }
        await SaveAsync(ct);
    }

    private async Task PrimaryClientsAsync(CancellationToken ct)
    {
        var step = Begin("Primary clients");
        var existing = await db.PrimaryClients.ToListAsync(ct);
        foreach (var p in existing) _usedClientIds[p.UniqueClientId] = $"primary client {p.Id}";
        foreach (var s in await db.SecondaryClients.Select(s => new { s.Id, s.UniqueClientId }).ToListAsync(ct))
            _usedClientIds[s.UniqueClientId] = $"secondary client {s.Id}";
        _unmatchedPrimaryId = existing.FirstOrDefault(p => Ids.Equals(p.UniqueClientId, UnmatchedPrimaryId))?.Id;
        var byLegacy = existing.Where(p => p.LegacyId != null).ToDictionary(p => p.LegacyId!.Value);

        foreach (var lp in await legacy.PrimaryClients.OrderBy(p => p.PrimaryClientId).ToListAsync(ct))
        {
            step.Read++;
            var isNew = !byLegacy.TryGetValue(lp.PrimaryClientId, out var e);
            if (e is null)
            {
                var clientId = ValidClientId(lp.UniqueClientId, out var problem);
                if (clientId is null || !_usedClientIds.TryAdd(clientId, $"legacy primary client {lp.PrimaryClientId}"))
                {
                    _report.Skip(step, "PrimaryClient", lp.PrimaryClientId,
                        problem ?? $"UniqueClientId {clientId} is already used by {_usedClientIds[clientId!]}");
                    continue;
                }
                e = new PrimaryClient { LegacyId = lp.PrimaryClientId, UniqueClientId = clientId, IsActive = true, CreatedDate = _now };
            }
            else
            {
                WarnIfClientIdChanged(step, "PrimaryClient", lp.PrimaryClientId, lp.UniqueClientId, e.UniqueClientId);
            }
            e.Name = Required(lp.ClientName, 100, step, "PrimaryClient", lp.PrimaryClientId, "ClientName");
            e.AutoProcess = lp.AutoProcess ?? false;
            e.LogoFileName = Optional(lp.LogoFilename, 255, step, "PrimaryClient", lp.PrimaryClientId, "LogoFilename");
            Track(step, e, isNew);
        }
        await SaveAsync(ct);
        _primaries = await db.PrimaryClients.Where(p => p.LegacyId != null).ToDictionaryAsync(p => p.LegacyId!.Value, p => p.Id, ct);
    }

    private async Task SecondaryClientsAsync(CancellationToken ct)
    {
        var step = Begin("Secondary clients");
        var existing = await db.SecondaryClients.ToListAsync(ct);
        var byLegacy = existing.Where(s => s.LegacyId != null).ToDictionary(s => s.LegacyId!.Value);
        foreach (var s in existing.Where(s => s.LegacyId == null && s.PrimaryClientId == _unmatchedPrimaryId && s.Name.StartsWith(PlaceholderNamePrefix)))
            _placeholders[s.Name] = s.Id;

        foreach (var ls in await legacy.SecondaryClients.OrderBy(s => s.SecondaryClientId).ToListAsync(ct))
        {
            step.Read++;
            var hasPrimary = ls.PrimaryClientId is { } legacyPrimary && _primaries.ContainsKey(legacyPrimary);
            var isNew = !byLegacy.TryGetValue(ls.SecondaryClientId, out var e);
            if (e is null)
            {
                var clientId = ValidClientId(ls.UniqueClientId, out var problem);
                if (clientId is null || !_usedClientIds.TryAdd(clientId, $"legacy secondary client {ls.SecondaryClientId}"))
                {
                    _report.Skip(step, "SecondaryClient", ls.SecondaryClientId,
                        (problem ?? $"UniqueClientId {clientId} is already used by {_usedClientIds[clientId!]}") + "; its batches go under " + UnmatchedPrimaryId);
                    continue;
                }
                e = new SecondaryClient { LegacyId = ls.SecondaryClientId, UniqueClientId = clientId, CreatedDate = _now };
                if (hasPrimary)
                {
                    e.PrimaryClientId = _primaries[ls.PrimaryClientId!.Value];
                    e.IsActive = true;
                }
                else
                {
                    e.PrimaryClientId = await UnmatchedPrimaryAsync(ct);
                    e.IsActive = false;
                    e.RequiresReview = true;
                    _report.Repair(step, "SecondaryClient", ls.SecondaryClientId,
                        $"PrimaryClientId {ls.PrimaryClientId?.ToString() ?? "(empty)"} was not imported; placed under {UnmatchedPrimaryId} for review");
                }
            }
            else
            {
                WarnIfClientIdChanged(step, "SecondaryClient", ls.SecondaryClientId, ls.UniqueClientId, e.UniqueClientId);
                if (hasPrimary && e.PrimaryClientId == _unmatchedPrimaryId && e.RequiresReview)
                {
                    e.PrimaryClientId = _primaries[ls.PrimaryClientId!.Value];
                    e.IsActive = true;
                    e.RequiresReview = false;
                }
            }
            e.Name = Required(ls.ClientName, 100, step, "SecondaryClient", ls.SecondaryClientId, "ClientName");
            Track(step, e, isNew);
        }
        await SaveAsync(ct);
        _secondaries = await db.SecondaryClients.Where(s => s.LegacyId != null).ToDictionaryAsync(s => s.LegacyId!.Value, s => s.Id, ct);
        foreach (var s in await db.SecondaryClients.Select(s => new { s.Id, s.UniqueClientId }).ToListAsync(ct))
            _secondaryByClientId[s.UniqueClientId] = s.Id;
    }

    private async Task BankAccountsAsync(CancellationToken ct)
    {
        var step = Begin("Client bank accounts");
        var accounts = await legacy.BankAccounts.ToDictionaryAsync(a => a.BankAccountId, ct);
        var links = (await legacy.PrimaryClientBankAccountTypes.ToListAsync(ct))
            .DistinctBy(l => (l.PrimaryClientId, l.BankAccountId, l.AccountTypeId)).ToList();
        var existing = await db.ClientBankAccounts.ToListAsync(ct);
        var wanted = new List<(ClientBankAccount Entity, bool IsNew, bool Active)>();

        foreach (var group in links.GroupBy(l => (l.PrimaryClientId, l.AccountTypeId)))
        {
            var valid = new List<(LegacyPrimaryClientBankAccountType Link, LegacyBankAccount Account, string AccountNo)>();
            foreach (var link in group)
            {
                step.Read++;
                var key = $"{link.PrimaryClientId}/{link.BankAccountId}/{link.AccountTypeId}";
                string? reason = null;
                LegacyBankAccount? account = null;
                var accountNo = (string?)null;
                if (link.PrimaryClientId is not { } lp || !_primaries.ContainsKey(lp)) reason = $"Primary client {link.PrimaryClientId} was not imported";
                else if (link.AccountTypeId is not ((int)AccountTypeCode.Nominated or (int)AccountTypeCode.Charges)) reason = $"AccountTypeId {link.AccountTypeId} is not Nominated (1) or Charges (2)";
                else if (link.BankAccountId is not { } ba || !accounts.TryGetValue(ba, out account)) reason = $"BankAccount {link.BankAccountId} does not exist";
                else if ((accountNo = Clean(account.AccountNo)) is null) reason = "Account number is empty";
                else if (accountNo.Length > 20) reason = "Account number is longer than 20 characters";
                if (reason is not null) { _report.Skip(step, "PrimaryClientBankAccountType", key, reason); continue; }
                valid.Add((link, account!, accountNo!));
            }
            if (valid.Count == 0) continue;

            var primaryId = _primaries[group.Key.PrimaryClientId!.Value];
            var type = (byte)group.Key.AccountTypeId!.Value;
            var payOpsActive = existing.Any(a => a.PrimaryClientId == primaryId && a.AccountTypeId == type && a.IsActive && a.LegacyId == null);
            var first = true;
            foreach (var (link, account, accountNo) in valid.OrderByDescending(v => v.Account.BankAccountId))
            {
                var key = $"{link.PrimaryClientId}/{link.BankAccountId}/{link.AccountTypeId}";
                var active = first && !payOpsActive;
                if (!first) _report.Repair(step, "PrimaryClientBankAccountType", key, "Client has more than one account of this type; older one imported as inactive");
                else if (payOpsActive) _report.Repair(step, "PrimaryClientBankAccountType", key, "An active account of this type was already set up in PayOps; imported as inactive");
                first = false;

                var e = existing.FirstOrDefault(a => a.PrimaryClientId == primaryId && a.AccountTypeId == type && a.LegacyId == account.BankAccountId);
                var isNew = e is null;
                e ??= new ClientBankAccount { LegacyId = account.BankAccountId, PrimaryClientId = primaryId, AccountTypeId = type, CreatedDate = _now };
                e.AccountName = Required(account.Description, 100, step, "BankAccount", account.BankAccountId, "Description");
                e.BranchCode = Required(account.BranchCode, 10, step, "BankAccount", account.BankAccountId, "BranchCode");
                e.AccountNumber = accountNo;
                e.GlAccountNumber = Optional(account.GLAccNo, 20, step, "BankAccount", account.BankAccountId, "GLAccNo");
                wanted.Add((e, isNew, active));
            }
        }

        // Deactivate first so the "one active account per type" index is never broken mid-save.
        foreach (var w in wanted.Where(w => !w.IsNew && !w.Active)) w.Entity.IsActive = false;
        await SaveAsync(ct);
        foreach (var w in wanted)
        {
            w.Entity.IsActive = w.Active;
            Track(step, w.Entity, w.IsNew);
        }
        await SaveAsync(ct);
    }

    private async Task ApiKeysAsync(CancellationToken ct)
    {
        var step = Begin("Client API keys");
        var keys = await db.ClientApiKeys
            .Select(k => new { k.KeyHash, k.PrimaryClientId, k.SecondaryClientId, Active = k.RevokedDate == null && (k.ExpiryDate == null || k.ExpiryDate > _now) })
            .ToListAsync(ct);
        var owners = keys.ToDictionary(k => Convert.ToHexString(k.KeyHash), k => (k.PrimaryClientId, k.SecondaryClientId));
        var activeCount = keys.Where(k => k.Active).GroupBy(k => (k.PrimaryClientId, k.SecondaryClientId)).ToDictionary(g => g.Key, g => g.Count());

        var sources = (await legacy.PrimaryClients.Where(p => p.AccessKey != null).Select(p => new { p.PrimaryClientId, p.AccessKey }).ToListAsync(ct))
            .Select(p => (Table: "PrimaryClient", LegacyKey: p.PrimaryClientId, p.AccessKey,
                Owner: _primaries.TryGetValue(p.PrimaryClientId, out var id) ? ((int?)id, (int?)null) : ((int?, int?)?)null))
            .Concat((await legacy.SecondaryClients.Where(s => s.AccessKey != null).Select(s => new { s.SecondaryClientId, s.AccessKey }).ToListAsync(ct))
            .Select(s => (Table: "SecondaryClient", LegacyKey: s.SecondaryClientId, s.AccessKey,
                Owner: _secondaries.TryGetValue(s.SecondaryClientId, out var id) ? ((int?)null, (int?)id) : ((int?, int?)?)null)));

        foreach (var source in sources)
        {
            var key = source.AccessKey!.Trim();
            if (key.Length == 0) continue;
            step.Read++;
            if (source.Owner is not { } owner)
            {
                _report.Skip(step, source.Table, source.LegacyKey, "Client was not imported, so its access key was not either");
                continue;
            }
            var hash = ApiKeyHasher.Hash(key);
            if (owners.TryGetValue(Convert.ToHexString(hash), out var current))
            {
                if (current == owner) step.Unchanged++;
                else _report.Skip(step, source.Table, source.LegacyKey, "The same access key is used by another client");
                continue;
            }
            if (activeCount.GetValueOrDefault(owner) >= 2)
            {
                _report.Skip(step, source.Table, source.LegacyKey, "Client already has two active API keys");
                continue;
            }
            db.ClientApiKeys.Add(new ClientApiKey
            {
                PrimaryClientId = owner.Item1, SecondaryClientId = owner.Item2, Name = "Legacy access key",
                KeyPrefix = "legacy-" + (key.Length >= 16 ? key[..4] : ""), KeyHash = hash, CreatedDate = _now
            });
            owners[Convert.ToHexString(hash)] = owner;
            activeCount[owner] = activeCount.GetValueOrDefault(owner) + 1;
            step.Inserted++;
        }
        await SaveAsync(ct);
    }

    private async Task LoadHeadersAsync(CancellationToken ct)
    {
        _headers = await legacy.Headers.OrderBy(h => h.HeaderId).ToListAsync(ct);
        foreach (var g in _headers.Where(h => Clean(h.BatchId) is not null).GroupBy(h => Clean(h.BatchId)!, Ids))
        {
            if (g.Count() == 1) _headerByBatchId[g.Key] = g.First().HeaderId;
            else _ambiguousBatchIds.Add(g.Key);
        }
        var stats = await legacy.Transactions.GroupBy(t => t.BatchId)
            .Select(g => new { BatchId = g.Key, Count = g.Count(), Amount = g.Sum(t => t.Amount ?? 0) })
            .ToListAsync(ct);
        _legacyPaymentsByBatchId = stats.Where(s => Clean(s.BatchId) is not null)
            .GroupBy(s => Clean(s.BatchId)!, Ids)
            .ToDictionary(g => g.Key, g => (g.Sum(s => s.Count), g.Sum(s => s.Amount)), Ids);
    }

    private async Task BankRunsAsync(CancellationToken ct)
    {
        var step = Begin("Bank runs");
        var runs = await legacy.BankRuns.OrderBy(r => r.BankRunId).ToListAsync(ct);
        if (runs.Count == 0) return;
        var bankId = await BankRunBankAsync(ct);
        var existing = await db.PaymentBatches.Where(b => b.LegacyId != null).ToDictionaryAsync(b => b.LegacyId!.Value, ct);
        var totals = _headers.Where(h => h.BankRunId != null).GroupBy(h => h.BankRunId!.Value)
            .ToDictionary(g => g.Key, g => (Records: g.Sum(h => h.Records ?? 0), Value: g.Sum(h => h.TotalValue ?? 0), First: g.Min(h => h.DateTrn)));

        foreach (var r in runs)
        {
            step.Read++;
            var total = totals.GetValueOrDefault(r.BankRunId);
            var reference = $"LEGACY-{r.BankRunId}";
            var status = r.StatusId is >= (int)BatchStatusCode.Sent and <= (int)BatchStatusCode.Duplicate ? (byte)r.StatusId : (byte)BatchStatusCode.Created;
            if (status == (byte)BatchStatusCode.Created) _report.Repair(step, "BankRun", r.BankRunId, $"StatusId {r.StatusId?.ToString() ?? "(empty)"} is not Sent/Ack/Nack/Duplicate; stored as Created");
            var runDate = r.DateTrn ?? total.First ?? _now;
            if (r.DateTrn is null) _report.Repair(step, "BankRun", r.BankRunId, "DateTrn is empty; used the earliest batch date");

            var isNew = !existing.TryGetValue(r.BankRunId, out var e);
            e ??= new PaymentBatch { LegacyId = r.BankRunId, BankId = bankId, Reference = reference, CreatedByUserId = _importUserId };
            e.Name = Fit(Clean(r.BankFileName) ?? reference, 255, step, "BankRun", r.BankRunId, "BankFileName");
            e.FileName = Optional(r.BankFileName, 255, step, "BankRun", r.BankRunId, "BankFileName");
            e.Comment = Optional(r.BankRunReason, 500, step, "BankRun", r.BankRunId, "BankRunReason");
            e.RunDate = runDate;
            e.TotalRecords = total.Records;
            e.TotalValue = Math.Round(total.Value, 2);
            e.BatchStatusId = status;
            e.ApprovalStatusId = (byte)ApprovalStatusCode.Approved;
            e.ApprovedDate = runDate;
            e.CreatedDate = runDate;
            e.ProcessedDate = status == (byte)BatchStatusCode.Created ? null : runDate;
            Track(step, e, isNew);
        }
        await SaveAsync(ct);
        _bankRuns = await db.PaymentBatches.Where(b => b.LegacyId != null).ToDictionaryAsync(b => b.LegacyId!.Value, b => b.Id, ct);
    }

    private async Task PaymentSummariesAsync(CancellationToken ct)
    {
        var step = Begin("Batches (Header)");
        var all = await db.PaymentSummaries.Select(s => new { s.SecondaryClientId, s.SequenceNumber, s.UniqueReference }).ToListAsync(ct);
        var sequences = all.Select(s => (s.SecondaryClientId, s.SequenceNumber)).ToHashSet();
        var references = all.Select(s => s.UniqueReference).ToHashSet(Ids);
        var existing = await db.PaymentSummaries.Where(s => s.LegacyId != null).ToDictionaryAsync(s => s.LegacyId!.Value, ct);
        var placeholderIds = _placeholders.Values.ToHashSet();
        var pending = 0;

        foreach (var h in _headers)
        {
            step.Read++;
            if (!IsServiceType(h.ServiceTypeId))
            {
                _report.Skip(step, "Header", h.HeaderId, $"ServiceTypeId {h.ServiceTypeId?.ToString() ?? "(empty)"} is not a known service type");
                continue;
            }
            if ((h.PaymentDate ?? h.DateTrn) is not { } paymentDate)
            {
                _report.Skip(step, "Header", h.HeaderId, "PaymentDate and DateTrn are both empty");
                continue;
            }
            if (h.PaymentDate is null) _report.Repair(step, "Header", h.HeaderId, "PaymentDate is empty; used DateTrn");

            var clientId = Clean(h.UniqueClientId);
            var secondaryId = clientId is not null && _secondaryByClientId.TryGetValue(clientId, out var sid) ? sid : (int?)null;
            var isNew = !existing.TryGetValue(h.HeaderId, out var e);
            if (secondaryId is null)
            {
                secondaryId = await PlaceholderAsync(clientId, ct);
                placeholderIds.Add(secondaryId.Value);
                _report.Repair(step, "Header", h.HeaderId, $"No secondary client with UniqueClientId {clientId ?? "(empty)"}; linked to a placeholder under {UnmatchedPrimaryId}");
            }

            var stats = Clean(h.BatchId) is { } batchId ? _legacyPaymentsByBatchId.GetValueOrDefault(batchId) : default;
            if (e is null)
            {
                var sequence = h.SequenceNumber ?? -h.HeaderId;
                if (h.SequenceNumber is null || !sequences.Add((secondaryId.Value, sequence)))
                {
                    _report.Repair(step, "Header", h.HeaderId, $"SequenceNumber {h.SequenceNumber?.ToString() ?? "(empty)"} is empty or already used by this client; stored as {-h.HeaderId}");
                    sequence = -h.HeaderId;
                    sequences.Add((secondaryId.Value, sequence));
                }
                var reference = Clean(h.UniquePaymentReference);
                if (reference is null || reference.Length > 100 || !references.Add(reference))
                {
                    var replacement = $"LEGACY-{h.HeaderId}";
                    _report.Repair(step, "Header", h.HeaderId, $"UniquePaymentReference {reference ?? "(empty)"} is empty or already used; stored as {replacement}");
                    reference = replacement;
                    references.Add(reference);
                }
                e = new PaymentSummary
                {
                    LegacyId = h.HeaderId, SecondaryClientId = secondaryId.Value, SequenceNumber = sequence,
                    UniqueReference = reference, FeeTotal = 0, CreatedByUserId = _importUserId
                };
            }
            else if (placeholderIds.Contains(e.SecondaryClientId) && !placeholderIds.Contains(secondaryId.Value)
                     && !sequences.Contains((secondaryId.Value, e.SequenceNumber)))
            {
                sequences.Add((secondaryId.Value, e.SequenceNumber));
                e.SecondaryClientId = secondaryId.Value;
            }

            if (h.BankRunId is { } runId && !_bankRuns.ContainsKey(runId))
                _report.Repair(step, "Header", h.HeaderId, $"BankRun {runId} does not exist; batch left without a bank run");
            e.PaymentBatchId = h.BankRunId is { } rid && _bankRuns.TryGetValue(rid, out var batchPk) ? batchPk : null;
            e.LegacyBatchId = Optional(h.BatchId, 50, step, "Header", h.HeaderId, "BatchId");
            e.ServiceTypeId = (byte)h.ServiceTypeId!.Value;
            e.PaymentDate = DateOnly.FromDateTime(paymentDate);
            e.RejectionAccountName = Optional(h.RejectionAccountName, 100, step, "Header", h.HeaderId, "RejectionAccountName");
            e.RejectionBranchCode = Optional(h.RejectionBranchCode, 10, step, "Header", h.HeaderId, "RejectionBranchCode");
            e.RejectionAccountNumber = Optional(h.RejectionAccountNumber, 20, step, "Header", h.HeaderId, "RejectionAccountNumber");
            if (h.Records is null) _report.Repair(step, "Header", h.HeaderId, "Records is empty; used the number of payments");
            if (h.TotalValue is null) _report.Repair(step, "Header", h.HeaderId, "TotalValue is empty; used the sum of the payments");
            e.TotalRecords = h.Records ?? stats.Count;
            e.TotalValue = Math.Round(h.TotalValue ?? stats.Amount, 2);
            e.ReconStatusId = h.ReconStatusId is >= (int)ReconStatusCode.Reconciled and <= (int)ReconStatusCode.Discrepancies
                ? (byte)h.ReconStatusId : (byte)ReconStatusCode.Unreconciled;
            e.CreatedDate = h.DateTrn ?? paymentDate;
            e.ModifiedDate = h.ModifiedDate;
            Track(step, e, isNew);
            if (++pending >= options.ChunkSize) { await SaveAsync(ct); pending = 0; }
        }
        await SaveAsync(ct);
        _summaries = await db.PaymentSummaries.Where(s => s.LegacyId != null).ToDictionaryAsync(s => s.LegacyId!.Value, s => s.Id, ct);
        db.ChangeTracker.Clear();
    }

    private async Task PaymentRecordsAsync(CancellationToken ct)
    {
        var step = Begin("Payments (Transactions)");
        var last = 0;
        while (true)
        {
            var chunk = await legacy.Transactions.Where(t => t.TransactionId > last).OrderBy(t => t.TransactionId).Take(options.ChunkSize).ToListAsync(ct);
            if (chunk.Count == 0) break;
            var (first, lastId) = (chunk[0].TransactionId, chunk[^1].TransactionId);
            last = lastId;
            var existing = await db.PaymentRecords.Where(r => r.LegacyId >= first && r.LegacyId <= lastId).ToDictionaryAsync(r => r.LegacyId!.Value, ct);

            foreach (var t in chunk)
            {
                step.Read++;
                var summaryId = SummaryForBatchId(t.BatchId, out var reason);
                if (summaryId is null) { _report.Skip(step, "Transactions", t.TransactionId, reason!); continue; }
                if (t.Amount is not { } amount || amount <= 0) { _report.Skip(step, "Transactions", t.TransactionId, "Amount must be greater than zero"); continue; }
                if (amount != Math.Round(amount, 2)) _report.Repair(step, "Transactions", t.TransactionId, $"Amount {amount} has more than 2 decimals; rounded");

                var status = t.TransactionStatusId is >= (int)TransactionStatusCode.Pending and <= (int)TransactionStatusCode.PendingUnreconciled
                    ? (byte)t.TransactionStatusId : (byte)TransactionStatusCode.Pending;
                if (status != t.TransactionStatusId) _report.Repair(step, "Transactions", t.TransactionId, $"TransactionStatusId {t.TransactionStatusId?.ToString() ?? "(empty)"} is not known; stored as Pending");

                var isNew = !existing.TryGetValue(t.TransactionId, out var e);
                e ??= new PaymentRecord { LegacyId = t.TransactionId, CreatedDate = t.DateTrn ?? _now };
                var statusChanged = !isNew && e.TransactionStatusId != status;
                e.PaymentSummaryId = summaryId.Value;
                e.BankFileReference = Optional(t.BankFileReference, 100, step, "Transactions", t.TransactionId, "BankFileReference");
                e.EmployeeCode = Required(t.EmployeeCode, 50, step, "Transactions", t.TransactionId, "EmployeeCode");
                e.Surname = Required(t.EmployeeSurname, 100, step, "Transactions", t.TransactionId, "EmployeeSurname");
                e.FirstName = Optional(t.EmployeeFirstname, 100, step, "Transactions", t.TransactionId, "EmployeeFirstname");
                e.Initials = Optional(t.EmployeeInitials, 10, step, "Transactions", t.TransactionId, "EmployeeInitials");
                e.IdOrPassport = Optional(t.EmployeeIdOrPassport, 50, step, "Transactions", t.TransactionId, "EmployeeIdOrPassport");
                e.Cell = Optional(t.EmployeeCell, 20, step, "Transactions", t.TransactionId, "EmployeeCell");
                e.Email = Optional(t.EmployeeEmail, 255, step, "Transactions", t.TransactionId, "EmployeeEmail");
                e.BranchCode = Required(t.BranchCode, 10, step, "Transactions", t.TransactionId, "BranchCode");
                e.AccountNumber = Required(t.AccountNumber, 20, step, "Transactions", t.TransactionId, "AccountNumber");
                e.AccountType = Required(t.AccountType, 10, step, "Transactions", t.TransactionId, "AccountType");
                e.Amount = Math.Round(amount, 2);
                e.BankReference = Optional(t.BankReference, 100, step, "Transactions", t.TransactionId, "BankReference");
                e.TransactionStatusId = status;
                e.RejectionReason = Optional(t.RejectionReason, 500, step, "Transactions", t.TransactionId, "RejectionReason");
                e.ModifiedDate = t.ModifiedDate;
                Track(step, e, isNew);
                if (isNew || statusChanged)
                {
                    db.PaymentRecordStatusHistories.Add(new PaymentRecordStatusHistory
                    {
                        PaymentRecord = e, TransactionStatusId = status, ChangedByUserId = _importUserId,
                        ChangedDate = t.ModifiedDate ?? t.DateTrn ?? _now,
                        Reason = isNew ? "Imported from legacy" : "Status updated from legacy"
                    });
                }
            }
            await SaveAsync(ct);
            db.ChangeTracker.Clear();
            log.WriteLine($"  payments: {step.Read:N0} read");
        }
    }

    private async Task RejectionsAsync(CancellationToken ct)
    {
        var step = Begin("Rejections");
        var latest = (await legacy.Rejections.Where(r => r.TransactionId != null).GroupBy(r => r.TransactionId)
            .Select(g => g.Max(r => r.RejectionId)).ToListAsync(ct)).ToHashSet();
        var last = 0;
        while (true)
        {
            var chunk = await legacy.Rejections.Where(r => r.RejectionId > last).OrderBy(r => r.RejectionId).Take(options.ChunkSize).ToListAsync(ct);
            if (chunk.Count == 0) break;
            last = chunk[^1].RejectionId;
            var transactionIds = chunk.Where(r => r.TransactionId != null).Select(r => (int?)r.TransactionId).Distinct().ToList();
            var records = await db.PaymentRecords.Where(r => transactionIds.Contains(r.LegacyId))
                .Select(r => new { LegacyId = r.LegacyId!.Value, r.Id }).ToDictionaryAsync(r => r.LegacyId, r => r.Id, ct);
            var recordIds = records.Values.ToList();
            var existing = await db.PaymentRejections.Where(r => recordIds.Contains(r.PaymentRecordId)).ToDictionaryAsync(r => r.PaymentRecordId, ct);

            foreach (var r in chunk)
            {
                step.Read++;
                if (r.TransactionId is not { } transactionId) { _report.Skip(step, "Rejections", r.RejectionId, "TransactionId is empty"); continue; }
                if (!latest.Contains(r.RejectionId)) { _report.Skip(step, "Rejections", r.RejectionId, "A later rejection exists for the same transaction"); continue; }
                if (!records.TryGetValue(transactionId, out var recordId)) { _report.Skip(step, "Rejections", r.RejectionId, $"Transaction {transactionId} was not imported"); continue; }
                if (r.Amount is not { } amount || amount <= 0) { _report.Skip(step, "Rejections", r.RejectionId, "Amount must be greater than zero"); continue; }

                var isNew = !existing.TryGetValue(recordId, out var e);
                if (e is not null && e.LegacyId != r.RejectionId)
                {
                    _report.Skip(step, "Rejections", r.RejectionId, "The payment already has a rejection recorded in PayOps");
                    continue;
                }
                e ??= new PaymentRejection { LegacyId = r.RejectionId, PaymentRecordId = recordId, CreatedDate = r.ModifiedDate ?? _now };
                if (r.BankRunId is { } runId && !_bankRuns.ContainsKey(runId))
                    _report.Repair(step, "Rejections", r.RejectionId, $"BankRun {runId} does not exist; rejection left without a bank run");
                e.PaymentBatchId = r.BankRunId is { } rid && _bankRuns.TryGetValue(rid, out var batchPk) ? batchPk : null;
                e.ServiceTypeId = IsServiceType(r.ServiceTypeId) ? (byte)r.ServiceTypeId!.Value : null;
                e.AccountName = Required(r.AccountName, 100, step, "Rejections", r.RejectionId, "AccountName");
                e.BranchCode = Required(r.BranchCode, 10, step, "Rejections", r.RejectionId, "BranchCode");
                e.AccountNumber = Required(r.AccountNumber, 20, step, "Rejections", r.RejectionId, "AccountNumber");
                e.Amount = Math.Round(amount, 2);
                e.BankReference = Optional(r.BankReference, 100, step, "Rejections", r.RejectionId, "BankReference");
                e.BankFileReference = Optional(r.BankFileReference, 100, step, "Rejections", r.RejectionId, "BankFileReference");
                e.TransactionStatusId = r.TransactionStatusId is >= (int)TransactionStatusCode.Pending and <= (int)TransactionStatusCode.PendingUnreconciled
                    ? (byte)r.TransactionStatusId : (byte)TransactionStatusCode.PendingUnreconciled;
                e.RejectionReason = Optional(r.RejectionReason, 500, step, "Rejections", r.RejectionId, "RejectionReason");
                e.ModifiedDate = r.ModifiedDate;
                Track(step, e, isNew);
            }
            await SaveAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private async Task CommentsAsync(CancellationToken ct)
    {
        var step = Begin("Batch comments");
        var existing = await db.PaymentSummaryComments.Where(c => c.LegacyId != null).ToDictionaryAsync(c => c.LegacyId!.Value, ct);
        foreach (var c in await legacy.HeaderComments.OrderBy(c => c.CommentId).ToListAsync(ct))
        {
            step.Read++;
            if (!_summaries.TryGetValue(c.HeaderId, out var summaryId)) { _report.Skip(step, "HeaderComment", c.CommentId, $"Header {c.HeaderId} was not imported"); continue; }
            var isNew = !existing.TryGetValue(c.CommentId, out var e);
            e ??= new PaymentSummaryComment { LegacyId = c.CommentId, CreatedByUserId = _importUserId };
            e.PaymentSummaryId = summaryId;
            e.Comment = Required(c.Comment, 1000, step, "HeaderComment", c.CommentId, "Comment");
            e.CreatedDate = c.CommentDate;
            Track(step, e, isNew);
        }
        await SaveAsync(ct);
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        async Task Count(string item, Task<int> legacyCount, Task<int> payOpsCount)
            => _report.Totals.Add(new TotalLine(item, await legacyCount, await payOpsCount));

        await Count("Primary clients", legacy.PrimaryClients.CountAsync(ct), db.PrimaryClients.CountAsync(p => p.LegacyId != null, ct));
        await Count("Secondary clients", legacy.SecondaryClients.CountAsync(ct), db.SecondaryClients.CountAsync(s => s.LegacyId != null, ct));
        await Count("Bank runs", legacy.BankRuns.CountAsync(ct), db.PaymentBatches.CountAsync(b => b.LegacyId != null, ct));
        await Count("Batches", legacy.Headers.CountAsync(ct), db.PaymentSummaries.CountAsync(s => s.LegacyId != null, ct));
        await Count("Payments", legacy.Transactions.CountAsync(ct), db.PaymentRecords.CountAsync(r => r.LegacyId != null, ct));
        _report.Totals.Add(new TotalLine("Payment amount", Math.Round(await legacy.Transactions.SumAsync(t => t.Amount ?? 0, ct), 2),
            await db.PaymentRecords.Where(r => r.LegacyId != null).SumAsync(r => r.Amount, ct)));
        _report.Totals.Add(new TotalLine("Batch total value", Math.Round(await legacy.Headers.SumAsync(h => h.TotalValue ?? 0, ct), 2),
            await db.PaymentSummaries.Where(s => s.LegacyId != null).SumAsync(s => s.TotalValue, ct)));
        await Count("Rejections", legacy.Rejections.CountAsync(ct), db.PaymentRejections.CountAsync(r => r.LegacyId != null, ct));
        await Count("Batch comments", legacy.HeaderComments.CountAsync(ct), db.PaymentSummaryComments.CountAsync(c => c.LegacyId != null, ct));

        var payOps = (await db.PaymentRecords.Where(r => r.LegacyId != null).GroupBy(r => r.PaymentSummaryId)
            .Select(g => new { g.Key, Count = g.Count(), Amount = g.Sum(r => r.Amount) }).ToListAsync(ct))
            .ToDictionary(g => g.Key, g => (g.Count, g.Amount));
        var rows = _headers.Select(h =>
        {
            var legacyStats = Clean(h.BatchId) is { } b && !_ambiguousBatchIds.Contains(b) ? _legacyPaymentsByBatchId.GetValueOrDefault(b) : default;
            var imported = _summaries.TryGetValue(h.HeaderId, out var sid);
            var payOpsStats = imported ? payOps.GetValueOrDefault(sid) : default;
            return (Header: h, Imported: imported, Legacy: (legacyStats.Count, Amount: Math.Round(legacyStats.Amount, 2)), PayOps: payOpsStats);
        }).ToList();

        foreach (var r in rows.Where(r => !r.Imported || r.Legacy != r.PayOps))
            _report.BatchDifferences.Add(new BatchDifference(r.Header.HeaderId, r.Header.BatchId, r.Header.UniqueClientId,
                r.Legacy.Count, r.Legacy.Amount, r.PayOps.Count, r.PayOps.Amount));
        foreach (var g in rows.GroupBy(r => Clean(r.Header.UniqueClientId) ?? "(empty)", Ids).OrderBy(g => g.Key, Ids))
            _report.Clients.Add(new ClientTotal(g.Key, g.Count(), g.Sum(r => r.Legacy.Count), g.Sum(r => r.Legacy.Amount),
                g.Count(r => r.Imported), g.Sum(r => r.PayOps.Count), g.Sum(r => r.PayOps.Amount)));
    }

    private int? SummaryForBatchId(string? batchId, out string? reason)
    {
        reason = null;
        var id = Clean(batchId);
        if (id is null) reason = "BatchId is empty";
        else if (_ambiguousBatchIds.Contains(id)) reason = $"BatchId {id} matches more than one Header";
        else if (!_headerByBatchId.TryGetValue(id, out var headerId)) reason = $"No Header has BatchId {id}";
        else if (!_summaries.TryGetValue(headerId, out var summaryId)) reason = $"Header {headerId} was not imported";
        else return summaryId;
        return null;
    }

    private async Task<int> UnmatchedPrimaryAsync(CancellationToken ct)
    {
        if (_unmatchedPrimaryId is { } id) return id;
        var primary = new PrimaryClient
        {
            UniqueClientId = UnmatchedPrimaryId, Name = "Legacy batches with no matching client", IsActive = false, CreatedDate = _now
        };
        db.PrimaryClients.Add(primary);
        await db.SaveChangesAsync(ct);
        _usedClientIds[UnmatchedPrimaryId] = $"primary client {primary.Id}";
        return (_unmatchedPrimaryId = primary.Id).Value;
    }

    private async Task<int> PlaceholderAsync(string? legacyClientId, CancellationToken ct)
    {
        var name = (PlaceholderNamePrefix + (legacyClientId ?? "(empty)"))[..Math.Min(100, PlaceholderNamePrefix.Length + (legacyClientId ?? "(empty)").Length)];
        if (_placeholders.TryGetValue(name, out var id)) return id;
        var clientId = ValidClientId(legacyClientId, out _);
        if (clientId is null || _usedClientIds.ContainsKey(clientId))
        {
            do clientId = $"LEGACY-ORPHAN-{++_placeholderCounter}"; while (_usedClientIds.ContainsKey(clientId));
        }
        var placeholder = new SecondaryClient
        {
            PrimaryClientId = await UnmatchedPrimaryAsync(ct), UniqueClientId = clientId, Name = name,
            IsActive = false, RequiresReview = true, CreatedDate = _now
        };
        db.SecondaryClients.Add(placeholder);
        await db.SaveChangesAsync(ct);
        _usedClientIds[clientId] = $"placeholder secondary client {placeholder.Id}";
        return _placeholders[name] = placeholder.Id;
    }

    private async Task<int> BankRunBankAsync(CancellationToken ct)
    {
        var banks = await legacy.Banks.OrderBy(b => b.BankId).ToListAsync(ct);
        var chosen = banks.FirstOrDefault(b => b.Name?.Contains("nedbank", StringComparison.OrdinalIgnoreCase) == true) ?? banks.FirstOrDefault();
        if (chosen is not null && _banks.TryGetValue(chosen.BankId, out var id)) return id;
        var nedbank = await db.Banks.FirstOrDefaultAsync(b => b.Code == "NEDBANK", ct);
        if (nedbank is null)
        {
            nedbank = new Bank { Code = "NEDBANK", Name = "Nedbank", IsActive = true };
            db.Banks.Add(nedbank);
            await db.SaveChangesAsync(ct);
        }
        return nedbank.Id;
    }

    private StepCounts Begin(string name)
    {
        log.WriteLine(name);
        return _report.Step(name);
    }

    private void Track<T>(StepCounts step, T entity, bool isNew) where T : class
    {
        if (isNew) { db.Add(entity); step.Inserted++; return; }
        var entry = db.Entry(entity);
        entry.DetectChanges();
        if (entry.State == EntityState.Modified) step.Updated++; else step.Unchanged++;
    }

    private Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private void WarnIfClientIdChanged(StepCounts step, string table, int key, string? legacyValue, string payOpsValue)
    {
        if (!Ids.Equals(Clean(legacyValue), payOpsValue))
            _report.Repair(step, table, key, $"UniqueClientId changed in legacy to {Clean(legacyValue) ?? "(empty)"}; PayOps keeps {payOpsValue} because it cannot change");
    }

    private string Required(string? value, int max, StepCounts step, string table, object key, string field)
    {
        var clean = Clean(value);
        if (clean is not null) return Fit(clean, max, step, table, key, field);
        _report.Repair(step, table, key, $"{field} is empty; stored as '{Missing}'");
        return Missing;
    }

    private string? Optional(string? value, int max, StepCounts step, string table, object key, string field)
        => Clean(value) is { } clean ? Fit(clean, max, step, table, key, field) : null;

    private string Fit(string value, int max, StepCounts step, string table, object key, string field)
    {
        if (value.Length <= max) return value;
        _report.Repair(step, table, key, $"{field} is longer than {max} characters; cut to {max}");
        return value[..max];
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ValidClientId(string? value, out string? problem)
    {
        var clean = Clean(value);
        problem = clean is null ? "UniqueClientId is empty"
            : clean.Length > 20 ? $"UniqueClientId {clean} is longer than 20 characters"
            : clean.Any(char.IsWhiteSpace) ? $"UniqueClientId {clean} contains spaces"
            : null;
        return problem is null ? clean : null;
    }

    private static bool IsServiceType(int? id) => id is >= (int)ServiceTypeCode.SDV and <= (int)ServiceTypeCode.DW;

    private static string RateKey(LegacyBillingRate r) => $"{r.DebtorsCode}/{r.ServiceTypeId}/{r.DateEffective:yyyy-MM-dd}";

    private static string BankCode(string name, int legacyId, HashSet<string> taken)
    {
        var code = new string(name.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (code.Length == 0) code = "BANK";
        if (code.Length > 20) code = code[..20];
        if (!taken.Contains(code)) return code;
        var suffix = $"-L{legacyId}";
        return code[..Math.Min(code.Length, 20 - suffix.Length)] + suffix;
    }
}
