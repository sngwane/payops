# PayOps – Legacy data copy (BulkPayments → PayOps) – plan for approval

Status: **PLAN ONLY**. Nothing is built until you approve.

## 1. Goal
Put the real legacy clients, batches and payments into the new `PayOps` database. This lets you check the Client and Batch File APIs in Swagger against real data, and it keeps the full payment history for later cut-over.

The legacy `BulkPayments` database stays the master. It is only **read**, never written to.

## 2. How it works
- **A new console tool, `tools/PayOps.LegacyImport`** (.NET 10).
  - It reads from a small read-only `LegacyDbContext` mapped to the legacy tables.
  - It writes through `PayOpsDbContext`, using EF Core and LINQ, with no stored procedures.
- **It is safe to re-run.** Every copied row remembers its legacy key, so a second run updates the rows that changed and adds new ones. It never creates duplicates.
- **`--dry-run`** reads everything, applies every mapping rule and prints the report, but writes nothing.
- **Order of steps:** lookups and holidays → banks and bank profiles → primary clients → secondary clients (plus the `LEGACY-UNMATCHED` placeholders) → bank accounts → API keys → batches → payments → bank runs → rejections → comments.
  - Each step is saved in chunks (for example 5,000 payments per transaction), so a failure can be resumed.
- **Report at the end:** legacy vs PayOps row counts and money totals per table, per client and per batch, plus a CSV listing every row that was fixed up or skipped, with the reason.
- **Where it runs:** on your PC. It reads `BulkPayments` on `DESKTOP-61GF5TP\SQLEXPRESS` and writes to `PayOps` on SQLEXPRESS, then to Docker `pg-schema-test`. A backup is taken first, the same as the migrations.

## 3. Schema change (one new migration: `LegacyImport`)
New nullable `LegacyId` columns, each with a filtered unique index (`WHERE LegacyId IS NOT NULL`), on these tables:
`PrimaryClient`, `SecondaryClient`, `ClientBankAccount`, `PaymentSummary`, `PaymentRecord`, `PaymentBatch`, `PaymentRejection`, `PaymentSummaryComment`.

`PaymentSummary` also gets `LegacyBatchId varchar(50)`, because legacy `Transactions` link to `Header` through the text `BatchId` and not through `HeaderId`.

These columns are also what the later Change-Tracking sync and the cut-over will use to match rows.

One system user, `legacy.import`, is added to `AppUser`. It fills the required "created by" fields on imported rows.

## 4. Mapping (legacy → PayOps)

| Legacy | PayOps | Notes |
|---|---|---|
| `PrimaryClient` | `PrimaryClient` | `ClientName`→`Name`, `UniqueClientId`, `AutoProcess`, `LogoFilename`. `IsActive = 1`. |
| `SecondaryClient` | `SecondaryClient` | `ClientName`, `UniqueClientId`, `PrimaryClientId` (through `LegacyId`). `NotificationXml` is kept for the Notifications API (see decision 6). |
| *(orphan batches)* | `SecondaryClient` under inactive primary `LEGACY-UNMATCHED` | One placeholder for each unmatched `Header.UniqueClientId` (11 batches), with `RequiresReview = 1`. This is the decision you already approved. |
| `BankAccount` + `PrimaryClientBankAccountType` | `ClientBankAccount` | `Description`→`AccountName`, `BranchCode`, `AccountNo`, `GLAccNo`, `AccountTypeId` (1 Nominated, 2 Charges). If there is more than one per type, only the latest is active. |
| `PrimaryClient.AccessKey` / `SecondaryClient.AccessKey` | `ClientApiKey` | Only the **SHA-256 hash** is stored. The plaintext is never stored. See decision 3. |
| `Header` | `PaymentSummary` | `UniqueClientId`→secondary, `ServiceTypeId`, `PaymentDate` (date only), `Rejection*`, `UniquePaymentReference`→`UniqueReference`, `SequenceNumber`, `Records`→`TotalRecords`, `TotalValue`, `ReconStatusId`, `DateTrn`→`CreatedDate`, `BankRunId`→`PaymentBatchId`. `FeeTotal = 0`. |
| `Transactions` | `PaymentRecord` | Linked through `BatchId`. The employee fields, `BranchCode`, `AccountNumber`, `AccountType`, `Amount`, `BankReference`, `BankFileReference`, `TransactionStatusId` and `RejectionReason` are copied. One `PaymentRecordStatusHistory` row per payment holds its current status, with the reason "Imported from legacy". |
| `BankRun` | `PaymentBatch` | `BankFileName`→`FileName`, `FileSequenceNumber`→`Reference`, `DateTrn`→`RunDate`, `StatusId`→`BatchStatusId`. `ApprovalStatus = Approved`, because legacy had no second approver. Created by `legacy.import`. |
| `Rejections` | `PaymentRejection` | Linked through `TransactionId`→`PaymentRecord`. |
| `HeaderComment` | `PaymentSummaryComment` | |
| `PublicHoliday` | `PublicHoliday` | Legacy has no name, so the name is set to "Public holiday". |
| `BankClientProfile`, `Bank` | `BankClientProfile`, `Bank` | Copied as they are. The running and daily sequence numbers matter for the Bank File API. |
| `BillingRates` | `BillingRate` | See decision 7. |

**Not copied** (they stay in legacy for reference):
- `Header_Rej` / `Transactions_Rej` / `Rejections_Rej`, `Header_Audit`, `ActivityLog`, `ErrorLog`.
- `Dispatch` (sent messages) and `NotificationTemplate`. These are for the Notifications phase.
- `ReconciledBatches`, which the Statements API keeps owning (decision 8 of the Phase 2 plan).
- `Wingate*`, `Settings`, `TableColumns`, `HaltIndicator`, `BillingMaintenance`.

## 5. Bad or missing legacy data
Legacy columns are mostly nullable, but PayOps requires values. So the first step of the build is a **read-only profiling query** on BulkPayments, run on your PC. It counts:
- NULL or over-long values in required fields;
- duplicate `UniqueClientId`s, including a primary and a secondary sharing one, which PayOps forbids;
- duplicate `UniquePaymentReference`s and `SequenceNumber`s;
- transactions without a header;
- bank runs and rejections whose links are broken.

I'll show you the numbers before any data is written. The default rules are in decision 5.

## 6. Tests and checks
- Testcontainers tests that seed a small legacy schema with known bad rows (orphans, NULLs, duplicates, a primary/secondary ID clash). They check that the mappings hold, that a second run changes nothing, and that the dry run writes nothing.
- On your data: a dry run first, then the real run. After that, the report must show equal counts and totals for every client and batch, or explain each difference in the CSV.
- Afterwards, a short check in Swagger: list clients, open a batch with its payments, create an API key and send a test batch.

## 7. Delivery
1. A PR with the tool, the `LegacyImport` migration, the tests and the README. CI must be green.
2. After you merge: apply the migration to both PayOps databases (with backups first), run the profiling query, and send you the numbers.
3. After you confirm: a dry run, then the real run on SQLEXPRESS and Docker, then the report.

## 8. Decisions needed (my recommendation first)
1. **Tool:** a .NET console tool using LINQ (recommended; it follows your no-procs rule) — or a T-SQL `INSERT…SELECT` script.
2. **Tracking legacy rows:** `LegacyId` columns on the PayOps tables (recommended; simple, and needed for the later sync) — or a separate `LegacyIdMap` table.
3. **Legacy access keys:** import them as hashed `ClientApiKey` rows so existing client systems keep working at cut-over (recommended) — or don't import them and issue new keys to every client.
4. **What to copy:** the full history in the table above (recommended) — or clients and bank accounts only for now.
5. **Bad rows:**
   - A missing *required text* gets a placeholder (for example a missing employee surname becomes `"(missing)"`) and is listed in the report.
   - A *duplicate client ID* or *broken link* is skipped and listed for you to fix in legacy before re-running.
   - The alternative is to skip every bad row.
6. **Secondary `NotificationXml` (client contacts):** keep it untouched in legacy now and import it with the Notifications API, which adds the client-contacts table (recommended) — or add that table now.
7. **`BillingRates.DebtorsCode`:** please tell me what it means. PayOps `BillingRate` is per service type, not per client. My recommendation is to import the latest rate per service type, and to raise per-client rates as a change if `DebtorsCode` is really a client.
8. **Re-runs:** run it on demand until cut-over (recommended), with a scheduled Change-Tracking sync to come later with the Worker — or run it once only.
