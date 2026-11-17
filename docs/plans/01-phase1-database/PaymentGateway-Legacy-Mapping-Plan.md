# PaymentGateway – Legacy-to-New Table Mapping Plan (for approval)

Source: read-only inventory of `BulkPayments` on DESKTOP-61GF5TP\SQLEXPRESS (40 tables, 89 procs, 1 trigger), code usage in `c:\repos\BulkPayments\src`, and the `Paygate` DB (19 tables).
Naming: SQL Server/.NET PascalCase singular, PK `Id`, FK `<Table>Id`, no table prefixes. Single shared `dbo` schema. Delivered later via EF Core 10 migrations.

## 1. How rejections work today (from `sp_Transactions_BulkUpdateIncoming` + `sp_Rejections_*`)
1. The bank response file marks an employee transaction **4 Rejected** (`Transactions.RejectionReason` set).
2. The proc inserts a **Rejections** row: the rejected amount is to be paid back into the client's **rejection account** (`Header.RejectionAccountName/BranchCode/AccountNumber`), status **5 PendingUnreconciled**.
3. Once reconciled → **1 Pending** → included in a later bank run (`BankRunId`, `BankFileReference`, `ServiceTypeId`) → **2 Transmitted** → **3 Success / 4 Rejected** from the next bank response.
4. Admin screens list pending (1) and pending-reconciliation (5) rejections.

=> Rejections are a refund payment flow, and **TransactionStatus 5 PendingUnreconciled is required** (it was left out of v3 because the supplied list was 1–4).

## 2. Proposed new tables (additions to v3)

| New table | Replaces (legacy) | Purpose / key columns |
|---|---|---|
| **PaymentRejection** | Rejections (306) | One refund per rejected record: `PaymentRecordId` (FK, unique), `PaymentBatchId` (FK null – bank run it is re-paid in), `ServiceTypeId`, `AccountName`, `BranchCode`, `AccountNumber` (snapshot of client rejection account), `Amount`, `BankReference`, `BankFileReference`, `TransactionStatusId` (default 5), `RejectionReason`, `CreatedDate`, `ModifiedDate` |
| **PaymentRecordStatusHistory** | (Paygate PaymentTransactionStatusHistory) | Audit trail of every record/rejection status change: `PaymentRecordId`, `PaymentRejectionId` null, `TransactionStatusId`, `PaymentBatchId` null, `Reason`, `ChangedDate` |
| **PaymentSummaryComment** | HeaderComment (2058) | Many dated comments per summary (v3 has a single `Comment` column) |
| *(temporal history)* | Header_Audit (4470) + trigger `trg_Header_Audit_Update` | Make `PaymentSummary` a SQL Server **system-versioned temporal table** – automatic history, no trigger |
| **Client** | PrimaryClient (2), SecondaryClient (244) | `ParentClientId` (null = primary), `UniqueClientId` (unique), `Name`, `AutoProcess`, `LogoFileName`, `IsActive`. `PaymentSummary.ClientId` becomes an FK to `Client.Id` |
| **ClientApiKey** | PrimaryClient/SecondaryClient.AccessKey | Access keys stored **hashed**, with expiry/revocation (today stored as plain text) |
| **ClientBankAccount** | BankAccount, BankAccountType, PrimaryClientBankAccountType | Client's own accounts: `ClientId`, `AccountTypeId` → AccountType (1 Nominated, 2 Charges), `AccountName`, `BranchCode`, `AccountNumber`, `GlAccountNumber` |
| **PaymentContact** + **ContactType** | PaymentContact (6633), ContactType (3), Header.NotificationXml | Who is notified per payment summary (name, email, number) |
| **Bank** | Bank (1) | Bank master (`Code`, `Name`); `PaymentBatch.BankId` FK added |
| **BankClientProfile** | BankClientProfile (1) | Bank file header data + `RunningSequence`/`DailySequence` used for bank file numbering |
| **Notification** | Dispatch (100) | Notification outbox: `NotificationChannelId`, `Recipient`, `Subject`, `Body`, `AttachmentPath`, `ReleaseDate`, `SentDate`, `StatusId`, `RetryCount`, `LastError` |
| **NotificationChannel** | MessageType (3) | Email / SMS / … |
| **NotificationTemplate** + **NotificationType** | NotificationTemplate (12), NotificationType (8) | Per-client templates (email subject/body, SMS body) |
| **BillingRate** (temporal) | BillingRates (3), BillingRatesAudit (6) | `ClientId`/`DebtorsCode`, `ServiceTypeId`, `Rate`, `EffectiveDate`; temporal table replaces the audit table |
| **Reconciliation** | ReconciledBatches (2) | Auto-reconciliation results linked to `PaymentSummaryId` (today written by the separate Statements API – see decision D5) |
| **PublicHoliday** | PublicHoliday (167) | Next-working-day calculation (`fn_GetNextworkingday`) |
| **AppSetting** | Settings (40), HaltIndicator (1) | Runtime-editable, non-secret settings incl. processing halt flag. Secrets move to configuration/secret store |
| **AuditLog** | ActivityLog (2323) | Who changed what (user, area, action, old/new value, client) |

## 3. Legacy tables already covered by v3
BankRun → PaymentBatch · BankRunStatus → BatchStatus · Header → PaymentSummary · Transactions → PaymentRecord · ReconStatus, ServiceType, TransactionStatus, AccountType → same-named lookups.

## 4. Legacy tables proposed to be dropped (not migrated as tables)
| Legacy | Rows | Reason |
|---|---|---|
| Transactions_Rej, Rejections_Rej | 104 each | One-off backup copies; archive once, no new table |
| Payment, PaymentTransaction (BulkPayments) | 2211 / 83906 | Duplicates of Header/Transactions, not used by this repo's code (see D4) |
| ErrorLog, Logs | 3 / 0 | Application logging moves to structured logging (Serilog → Seq/Application Insights), not the business DB |
| Status, TableColumns, BillingMaintenance | 0 each | Empty / unused |

## 5. Ideas worth reusing from the `Paygate` DB (it looks like an earlier modernization attempt)
- **Maker-checker on bank runs**: `ApprovalStatus`, `RequestedByUserId`, `ApprovedByUserId`, `ApprovedDate` on the batch → add to `PaymentBatch`.
- **Permission + RolePermission**: fine-grained permissions behind roles.
- **UserClient**: limits which clients a UI user can see.
- RefreshToken / MFA / lockout columns: only if login stays local (not Entra ID) – see D3.

## 6. Decisions needed
- **D1** Add TransactionStatus **5 PendingUnreconciled** (needed by the rejection flow)? *Recommended: yes.*
- **D2** Accept the new tables in section 2? (Can be phased: core payments/rejections first, then clients, notifications, billing.)
- **D3** Users/login: local passwords (adopt Paygate's MFA/lockout/refresh-token design) or Entra ID/AD sign-in? *Recommended: Entra ID; keep AppUser/Role/Permission/UserClient for authorization only.*
- **D4** Confirm `Payment`/`PaymentTransaction`/`PaymentContact` in BulkPayments: who writes them (another service?) – drop the two duplicates, keep PaymentContact.
- **D5** `ReconciledBatches` is written by the Statements API: move it into PaymentGateway (Statements API writes there) or leave it in legacy and only link by reference?
- **D6** Is `Paygate` your earlier attempt, and should its maker-checker/permissions design be adopted?

## 7. Delivery after approval
1. EF Core 10 solution (`src\PaymentGateway\`): scaffold v3, add the approved tables in C#, `InitialCreate` migration; temporal tables via EF Core `IsTemporal()`.
2. Prove the migration on a fresh Docker SQL Server 2022, then apply to the container and SQLEXPRESS (drop only if empty).
3. LINQ queries (batch get with client filter, Paid Away, History, pending rejections, pending-reconciliation rejections) + Testcontainers tests.
4. Separate later phase: data migration (ETL) legacy → PaymentGateway, with row-count and value reconciliation.
