# PaymentGateway DB – Corrected Schema Plan (Option C: single shared schema)

Status: **DRAFT – awaiting approval. No DDL written yet.**
Source reviewed: `c:\repos\BulkPayments\src\PaymentGateway Scripts.sql`

Scope: keep the draft's structure (PaymentBatches → PaymentSummaries → PaymentRecords + Users/Roles) in one shared `dbo` schema, make it run, and close the gaps. Items are tagged:
- **[FIX]**: needed for the script to run or be correct. Recommended, not optional.
- **[REC]**: recommended. Include unless you say no.
- **[OPT]**: your call (see Open Questions).

---

## 1. Conventions
| Item | Proposal |
|---|---|
| Schema | `dbo` (single shared schema, per your decision) |
| PK naming | `Id` on every table; FK columns named `<Entity>Id` (e.g. `PaymentBatchId`). Most draft tables already use `Id`, so only `PaymentBatches.PaymentBatchId` → `Id` changes. |
| Constraint names | Explicit names: `PK_`, `FK_<Child>_<Parent>`, `UQ_`, `IX_`, `DF_`, `CK_` (easier to deploy and diff) |
| Dates | `DATETIME2(3)` in UTC, defaulting to `SYSUTCDATETIME()`; `PaymentDate` stays `DATE` |
| Money | `DECIMAL(18,2)`, with `CHECK (Amount > 0)` on records |
| Strings | Sized columns instead of `NVARCHAR(MAX)` wherever the max length is known |
| Script | Re-runnable (`IF NOT EXISTS` / `CREATE OR ALTER`) and in dependency order: DB → lookups → security → clients → batches → summaries → records → supporting → indexes → seed data → procs |
| DB options | `READ_COMMITTED_SNAPSHOT ON`, so procs don't need `WITH (NOLOCK)` (avoids dirty/missing reads on payment data) |

## 2. Fixes to the draft [FIX]
1. Add `USE PaymentGateway;` after `CREATE DATABASE`, and replace `go;` with `GO`.
2. FK targets: `PaymentBatches(Id)`, `Users(Id)`, and `PaymentRecords.StatusId → TransactionStatuses(Id)` (the draft points at columns that don't exist).
3. Create the missing lookup tables, and create Users before the tables that reference it.
4. `PaymentSummaries.PaymentBatchId` → **NULLable**. A client batch exists before it is put into a bank file (the legacy `Header.BankRunId` was joined with LEFT JOIN for this reason).
5. `PaymentSummaries.StatusId` default → **2 (Unreconciled)**. The draft defaults to 1 = Reconciled.
6. Add `PaymentRecords.RejectionReason NVARCHAR(500) NULL` (the legacy proc returns it, but the draft has no column for it).
7. `PaymentRecords.Id` → `BIGINT` (high-volume table).
8. `Users.FailedLoginAttempts` → `NOT NULL DEFAULT 0`.
9. Rewrite `sp_PaymentBatch_Get` against the new tables (see §6).

## 3. Lookup tables [FIX] (fixed IDs, seeded, not IDENTITY)
| Table | Values | Used by |
|---|---|---|
| `BatchStatuses` | 1 Sent, 2 Ack, 3 Nack, 4 Duplicate *(+ 0 Created? – Q3)* | PaymentBatches.StatusId |
| `ReconStatuses` *(draft called it PaymentStatuses)* | 1 Reconciled, 2 Unreconciled, 3 Discrepancies | PaymentSummaries.StatusId |
| `TransactionStatuses` | 1 Pending, 2 Transmitted, 3 Success, 4 Rejected | PaymentRecords.StatusId |
| `ServiceTypes` | copied from the legacy lookup | PaymentSummaries.ServiceTypeId |
| `AccountTypes` | copied from the legacy lookup | PaymentRecords.AccountTypeId |

Columns: `Id TINYINT PK, Code NVARCHAR(30) UQ, Name NVARCHAR(100), IsActive BIT`.

## 4. Core tables

### 4.1 Users / UserRoles / UserRoleMappings
Same as the draft, with the FK fixes and named constraints. **Q2** decides whether `PasswordHash`, `FailedLoginAttempts` and `AccountLockedUntil` stay (local login) or are replaced by `ExternalId` (Entra ID/AD sign-in).

### 4.2 Clients [REC]
| Column | Type | Notes |
|---|---|---|
| Id | INT IDENTITY PK | |
| ClientCode | NVARCHAR(20) UQ | legacy `UniqueClientId` (e.g. `001104`) |
| ClientName | NVARCHAR(255) | |
| RejectionAccountName | NVARCHAR(100) NULL | moved here from PaymentSummaries |
| RejectionBranchCode | NVARCHAR(10) NULL | |
| RejectionAccountNumber | NVARCHAR(20) NULL | |
| IsActive | BIT DF 1 | |
| CreatedDate / ModifiedDate | DATETIME2(3) | |

### 4.3 PaymentBatches (one bank file / bank run)
`Id INT IDENTITY PK, BatchReference NVARCHAR(100) UQ (FileSequenceNumber), BatchName NVARCHAR(255), BatchDate DATETIME2(3), TotalRecords INT, TotalValue DECIMAL(18,2), StatusId TINYINT FK BatchStatuses, FileName NVARCHAR(255) NULL, Comment NVARCHAR(500) NULL (BankRunReason), CreatedByUserId INT FK Users, CreatedDate, ProcessedDate NULL`

### 4.4 PaymentSummaries (one client batch; replaces Header)
| Column | Type | Change vs draft |
|---|---|---|
| Id | INT IDENTITY PK | |
| PaymentBatchId | INT NULL FK PaymentBatches | now NULL |
| ClientId | INT FK Clients | replaces ClientId/ClientName text *(if Clients approved)* |
| ServiceTypeId | TINYINT FK ServiceTypes | |
| PaymentDate | DATE | |
| SequenceNumber | BIGINT | `UQ (ClientId, SequenceNumber)` |
| UniqueReference | NVARCHAR(100) UQ | replaces legacy `Header.BatchId` (GUID used as the lookup key) |
| TotalRecords | INT | was BIGINT |
| TotalValue | DECIMAL(18,2) | |
| Comment | NVARCHAR(500) NULL | was MAX |
| StatusId | TINYINT FK ReconStatuses DF 2 | default fixed |
| CreatedByUserId | INT NULL FK Users | NULL when created by the Client API (no human user) |
| CreatedDate / ModifiedDate | DATETIME2(3) | |

### 4.5 PaymentRecords (one employee payment; replaces Transactions)
Draft columns kept, with: `Id BIGINT`, `BranchCode NVARCHAR(10)`, `AccountNumber NVARCHAR(20)`, `StatusId TINYINT FK TransactionStatuses DF 1`, **+ `RejectionReason NVARCHAR(500) NULL`**, **+ `StatusDate DATETIME2(3)`** *(Q4)*, and `CHECK (Amount > 0)`.

## 5. Supporting tables [OPT – Q1]
- **BatchFiles** (Batch File API): `Id, ClientId, FileName, FileHash CHAR(64), ReceivedDate, StatusId (Received/Validated/Failed), ValidationErrors NVARCHAR(MAX), PaymentSummaryId NULL`, with `UQ (ClientId, FileHash)` for duplicate-file detection.
- **Notifications** (Notifications API, used as an outbox): `Id BIGINT, PaymentSummaryId NULL, PaymentRecordId NULL, Channel (Email/SMS), Recipient, Subject, Body, StatusId (Pending/Sent/Failed), Attempts, LastError, CreatedDate, SentDate`.
- **Status history / audit**: either (a) SQL Server **temporal tables** on PaymentBatches/Summaries/Records, which give full row history with no app code *(recommended)*, or (b) explicit `*StatusHistory` tables (`FromStatusId, ToStatusId, Reason, ChangedBy, ChangedDate`).
- **Protecting sensitive personal data (POPIA)** on `EmployeeIdOrPassport`, `AccountNumber`, `EmployeeCell`, `EmployeeEmail`: (a) Dynamic Data Masking for UI roles, which is cheap but is not encryption; (b) Always Encrypted, which is strong but those columns can no longer be searched with LIKE, range-queried or freely indexed. Recommended: (a) now, and (b) on IdOrPassport/AccountNumber if compliance requires it.

## 6. Indexes & stored procedures
**Indexes [REC]**
- `PaymentSummaries (PaymentBatchId)`, `(ClientId, PaymentDate)`, `(StatusId)`
- `PaymentRecords (PaymentSummaryId) INCLUDE (StatusId, Amount)`
- `PaymentRecords (StatusId, StatusDate)` for the Paid Away tab (Success today) and the History tab (Success in a date range)
- `PaymentRecords (BankReference)`, `PaymentBatches (StatusId, BatchDate)`

**Procs [FIX]**
- Replace `sp_PaymentBatch_Get` with `usp_PaymentSummary_GetByReference (@UniqueReference, @ClientCode)`. It returns the same 2 result sets (batch header, records) from the new tables. Changes:
  - Avoids the `sp_` prefix (SQL Server checks master first for `sp_` procs).
  - Drops NOLOCK (relies on RCSI instead).
  - **Filters by client**: the legacy proc accepts `@ClientId` but never uses it, so a caller can read another client's batch.

## 7. Out of scope (separate plans later)
- Migrating data from the legacy tables (Header/BankRun/Transactions → new tables, with a column-by-column mapping).
- API/EF Core models.

## 8. Validation after approval
- Run the script end-to-end on an empty SQL Server 2022 instance (a container on my VM, so nothing touches your DBs). Run it twice to prove it is re-runnable, then run the proc against seed data.
- Deliver the updated script to your machine (Q6).

## 9. Open questions
1. Which supporting items should be included: Clients [REC], BatchFiles, Notifications, history (temporal or tables), PII protection (DDM or Always Encrypted)?
2. Users: keep local passwords, or use Entra ID/AD sign-in?
3. Do PaymentBatches need a status for "Created / not yet sent" before 1 = Sent?
4. Which date marks a record as paid for the Paid Away/History tabs? I propose `StatusDate`, set whenever StatusId changes.
5. Does the Statements API's `ReconciledBatches` table live in this DB? If so, should it get an FK to PaymentSummaries?
6. Overwrite `PaymentGateway Scripts.sql`, or create a new file next to it?
7. OK to use `Id` for every PK, and to rename `PaymentStatuses` → `ReconStatuses`?
