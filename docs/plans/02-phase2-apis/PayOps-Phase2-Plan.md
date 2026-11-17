# PayOps Phase 2: the five APIs (plan for approval)

Repo: github.com/sngwane/payops (`main` @ b785a54). Nothing is built until you approve. Each step below is its own PR, and
each PR gets a short "final check" plan from me before I start it.

## 1. Where we are
| Step | Capability | Status |
|---|---|---|
| 2a | **Client API**: primary/secondary clients, bank accounts, API keys | **Done**: PR #2 merged, `ClientApi` migration on Docker + SQLEXPRESS |
| 2b | **Batch File API**: clients submit payment batches | Next |
| 2c | **Payments API**: ops view batches/records, reconciliation, rejections, Paid Away/History | Planned |
| 2d | **Bank File API + bank worker**: bank runs, maker-checker approval, Nedbank file, ACK/NACK/Unpaid files | Planned |
| 2e | **Notifications API + dispatcher**: templates, contacts, email/SMS outbox, scheduled reminders | Planned |

What the legacy system does today (read from `c:\repos\BulkPayments`), so every legacy duty has a new home:
| Legacy piece | What it does | New home |
|---|---|---|
| WCF `UploadBatch` / `Submit` (`BulkPaymentModule`) | access key -> XSD check -> field checks -> totals/records/date checks -> insert Header + Transactions -> queue upload confirmation | 2b Batch File API |
| WCF `Register` / `Update` / `ResetAccessKey` | client self-registration, notification contacts, key reset | 2a (done) + contacts in 2e |
| WCF `PaymentBatch` (`sp_PaymentBatch_Get`) | client reads back its batch | 2b (client view) / 2c (ops view) |
| WCF `Rejections` | stub only, never finished | 2b (client reads its rejections) |
| WCF `CreateBankFile` | disabled ("not available at the moment") | not carried over |
| `BankFileRunModule` / `NedbankFile` (Windows service timer, between `OutgoingFilesStartHour` and `OutgoingFilesEndHour`) | picks pending batches + pending rejection refunds, writes Nedbank fixed-width file (01 header / 02 lines / 03 trailer), bumps running/daily sequence, sets Transmitted | 2d |
| `IncomingFileModule` (Ack `<id>A`, Nack `<id>N`, Unpaid `<id>O`) | reads bank replies from a folder, sets Ack/Nack and per-line Success/Rejected, archives, emails ops | 2d |
| `OutgoingFileQueueWatcher` | alerts ops when the bank hasn't picked up a file in N minutes | 2d |
| `SendFundingRequestToManagers`, `UnReconciledNotification`, `PaymentConfirmation` | funding reminder, unreconciled report, daily payment confirmation PDF (skips weekends/public holidays) | 2e |
| `NotifyMgr` + `Dispatch` table, ConnectMobile SMS | email (SMTP) and SMS outbox | 2e |
| `PublicHoliday`, `BillingRates`, `Settings`, `HaltIndicator` | calendar, per-service-type fee, schedule settings | 2b (holidays, billing rates), config for the rest |

## 2. Cross-cutting design (applies to 2b-2e)
- **Same pattern as the Client API:** each API = `PayOps.<Name>.Data` (own small DbContext, LINQ only, no stored procs)
  + `PayOps.<Name>.Api` (minimal API, ProblemDetails, paging, `/health`, OpenAPI, Dockerfile). Only `PayOpsDbContext` owns
  migrations; one new migration per PR, always a new file; idempotent script run with `sqlcmd -I`.
- **Shared plumbing** moves into a new `src/Shared/PayOps.ServiceDefaults` (Entra auth setup, ProblemDetails, health, OpenAPI,
  paging) in 2b, so the five APIs don't copy it. The Client API is switched to it in the same PR (no behaviour change).
- **How the APIs talk to each other:** they share the one `PayOps` database (your decision), so they read each other's
  tables through their own contexts. No message broker yet. Work for another capability is written as a row in the same
  transaction (**outbox**): e.g. an accepted batch writes a `NotificationMessage` row, which 2e sends.
- **Background jobs** (bank runs, incoming files, reminders, sending email/SMS) run in one new **`PayOps.Worker`**
  (.NET Worker Service, replaces the two Windows services). Each job has a schedule in config and takes a DB lease row
  (`JobLease`) so two worker copies never run the same job at once.
- **Business calendar:** one shared `BusinessCalendar` service (Sundays + `PublicHoliday` table, plus Saturdays where the
  legacy job skipped them) used by batch validation, bank runs and reminders.
- **Sign-in:** staff use Entra ID roles; clients' systems use their API key (`X-Api-Key` header) validated by
  `ClientApiKeyService.ValidateApiKeyAsync` (built in 2a). New roles: `PayOps.Operator` (day-to-day ops),
  `PayOps.Approver` (bank-run checker), existing `PayOps.Reader`, `PayOps.ClientAdmin`.
- **Money and dates:** `decimal(18,2)`, payment dates are `date`, all timestamps UTC; South Africa time (SAST) is used
  only for cut-offs and schedules.
- **Tests:** every PR adds Testcontainers data tests + WebApplicationFactory API tests; CI must be green; migration tested on
  a copy with existing rows; idempotent script run twice.
- **Deployment:** after you merge each PR I apply its migration to Docker `pg-schema-test` and SQLEXPRESS from your PC
  (backup first), as done for 2a.

## 3. Step 2b: Batch File API (next PR)
### 3.1 Endpoints
| Method | Route | Who | Purpose |
|---|---|---|---|
| POST | /api/batches | client key | submit a batch (JSON) |
| POST | /api/batches/xml | client key | submit the **legacy XML** (same `BulkPaymentXml.xsd`), so existing clients and the later CoreWCF facade work unchanged |
| POST | /api/batches/validate | client key | dry run: same checks, nothing saved |
| GET | /api/batches?from=&to=&page= | client key | the client's own batches |
| GET | /api/batches/{id} | client key | batch + records + statuses (replaces WCF `PaymentBatch`) |
| GET | /api/batches/{id}/rejections | client key | rejected records and refund status (finishes the WCF `Rejections` stub) |
| GET | /api/uploads?secondaryClientId=&status= | staff | upload log incl. failed uploads, for support |
| GET/POST/PUT | /api/public-holidays, /api/billing-rates | staff (ClientAdmin) | maintain calendar and fees |

### 3.2 Validation (legacy rules kept, made explicit)
1. Key valid, client and its primary active; header `UniqueClientId` = the key's secondary client.
2. Schema: required header/payment/footer fields; lengths match the DB columns; branch code and account number digits only;
   amount > 0 with 2 decimals.
3. Footer `Records` = number of payments.
4. Footer `TotalValue` = sum of amounts, **or** sum + billing rate x records (legacy accepts both; decision 3).
5. Service type: SDV, RTL, OneDay, TwoDay, DW (legacy spellings like "1 DAY", "SAMEDAY" still accepted and mapped).
6. Payment date: SDV/RTL not in the past; OneDay > today; TwoDay >= today + 2 business days (legacy didn't check
   TwoDay; decision 5); never a Sunday or public holiday.
7. Duplicates: `UniqueReference` unique (already), `(SecondaryClient, SequenceNumber)` unique (already). A resubmission
   of the exact same reference returns **409 with the existing batch id**, so a client retry never creates a second batch.
8. All errors in one response: RFC 9457 problem details with a per-field list (`payments[12].accountNumber: ...`),
   replacing the legacy "XmlData with errors" field.
9. Size limit: up to 50,000 records / 10 MB per batch (configurable).

### 3.3 What gets saved (one transaction)
`PaymentSummary` (ReconStatus = Unreconciled) + all `PaymentRecord`s (Pending) + `PaymentRecordStatusHistory` +
`BatchUpload` log row + batch notification contacts + an "Upload confirmation" `NotificationMessage` (sent in 2e).
Records are inserted with EF Core batching (tested at 50,000 rows).

### 3.4 Migration `BatchFileApi`
| Table | Purpose |
|---|---|
| `BatchUpload` | every upload attempt: client, received date, format (Json/Xml), status (Accepted/Rejected), error JSON, SHA-256 of the payload, `PaymentSummaryId` when accepted |
| `PublicHoliday` | Date (unique), Name |
| `BillingRate` | ServiceTypeId, Rate, EffectiveFrom, EffectiveTo (fee history instead of one value) |
| `PaymentSummary` + `FeeTotal` | fees included in the client's footer total (0 when not) |
| `BatchContact` | per-batch contacts from the upload (Upload / Rejection / Funding, name, email, cell) |
| `NotificationMessage` | outbox row (created now, sent in 2e): channel, recipient, subject, body, status, attempts, related batch |
| `JobLease` | worker job locks (used from 2d) |

## 4. Step 2c: Payments API (ops view)
Builds on the read-only queries already in `PayOps.Payments.Data`.
- Batches: search by client, date, status, recon status; batch detail with records, comments (`PaymentSummaryComment`),
  temporal history ("who changed what" from `PaymentSummaryHistory`).
- Reconciliation: mark a batch Reconciled / Unreconciled / Discrepancies (manual), with comment; read the
  auto-reconciled list (legacy `ReconciledBatches`, written by your Statements API; decision 8).
- Holds: put a batch on hold / release before it is in a bank run.
- Paid Away (today) and History (date range) = records with status Success, as you defined.
- Rejections: list PendingUnreconciled -> ops confirm refund details -> Pending (picked up by the next bank run).
- Exports: CSV for every list.
- Migration `PaymentsApi`: `PaymentSummary.IsOnHold`, `HoldReason`; `PaymentSummaryComment` already exists.

## 5. Step 2d: Bank File API + bank jobs in `PayOps.Worker`
- **Bank run** (`PaymentBatch`): the worker builds a draft run on the legacy schedule (every N minutes between start and
  end hour on business days) from eligible batches + Pending rejection refunds.
  - Eligible batch: payment date due for its service type, not on hold, and **funded**: Reconciled, or the primary has
    `AutoProcess = 1` (decision 7).
  - **Maker-checker** (already in the schema): a second person with `PayOps.Approver` approves before any file is written
    (`ApprovedByUserId` <> `CreatedByUserId`). Rejecting a run releases its batches.
- **Outgoing file**: Nedbank fixed-width format exactly as `NedbankFile.cs` (01 header, 02 transaction, 03 trailer; Nominated
  / Charges account from `ClientBankAccount`, falling back to the bank profile), file name
  `<env><ClientIdentifier>I<daily seq 3>.D0.SQ320`, running and daily sequence in `BankClientProfile` updated in the same
  transaction. A golden-file test compares the output byte-for-byte with a file the legacy code produced (I'll need one
  real/anonymised legacy file from you).
- **Bank interface**: write to / read from configured folders as today (decision 9); bank files are also stored in the new
  `BankFile` table (content + SHA-256) so nothing depends on the folder.
- **Incoming files**: Ack -> run Ack and lines Success/Rejected; Nack -> run Nack, batches released; Unpaid -> line Rejected +
  `PaymentRejection` (PendingUnreconciled); "REDIRECT" lines skipped as legacy. Each file processed once (hash check).
- **Queue watcher**: alert if a file isn't collected within `MinutesForBankToClearOutgoingFile`.
- Endpoints: list/view runs, approve/reject run, download file, upload a bank reply manually, bank profiles.
- Only Nedbank is implemented (legacy FNB/Standard Bank/ABSA cases are empty). The file writer sits behind
  `IBankFileFormat` so another bank is one new class.
- Migration `BankFileApi`: `BankFile`, `BankClientProfile` + `NominatedAccountNumber` / `ChargesAccountNumber`, `PaymentBatch.RejectedReason`.

## 6. Step 2e: Notifications API + dispatcher
- **Templates** (`NotificationTemplate`): type (UploadConfirm, RejectConfirm, FundingRequest, PaymentConfirmation,
  Unreconciled, OpsAlert) x channel (Email/Sms); default templates seeded from the legacy `.txt` files with the same
  placeholders (`<Client.Name>`, `<Header.TotalValue>`, ...); an optional override per primary client.
- **Contacts** (`ClientContact`): per secondary client, per type (Upload / Rejection / Funding), name, email, cell
  (replaces the legacy notification XML from `Register`/`Update`). Batch contacts from 3.4 are used when present.
- **Dispatcher** (worker): sends `NotificationMessage` rows with retry and back-off (max 5 tries), marks Sent/Failed,
  never sends twice. Email via SMTP (MailKit), SMS via the ConnectMobile HTTP API as today; credentials from config/secrets.
- **Scheduled notifications** (worker, business days only): funding reminder (hourly in the bank window), unreconciled
  report (at `UnreconciledNotificationHour`), payment confirmation (at `PaymentConfirmationSendHour`) with PDF attachment.
- Endpoints: templates CRUD + preview, contacts CRUD, message log/search, resend failed message.
- Migration `NotificationsApi`: `NotificationTemplate`, `ClientContact`, `NotificationAttachment`.

## 7. Decisions needed (my recommendation first)
| # | Question | Recommended | Alternative |
|---|---|---|---|
| 1 | Build order | 2b Batch File -> 2c Payments -> 2d Bank File -> 2e Notifications (messages queue up in the outbox until 2e) | Notifications before Bank File |
| 2 | Upload formats | JSON **and** legacy XML (same XSD) | JSON only (XML only through the later CoreWCF facade) |
| 3 | Footer total incl. billing fee | Accept both like legacy; store the fee in `PaymentSummary.FeeTotal` | Accept only the sum of payments |
| 4 | Notification contacts | Client-level contacts (Client/Notifications API) + per-batch contacts from the upload override them | Per-batch only (legacy) |
| 5 | Payment-date rules | Legacy rules + also check TwoDay (>= 2 business days) | Legacy rules exactly |
| 6 | Failed uploads | Logged in `BatchUpload` with errors (not the payload) | Not stored |
| 7 | Which batches go into a bank run | Reconciled (funded), or primary `AutoProcess = 1`; never on hold. **Please confirm this is what AutoProcess means** | All pending batches regardless of funding (I have not yet checked which rule the legacy `GetPendingBankRunData` query applies) |
| 8 | Auto-reconciliation from your Statements API | Read-only for now; Statements API keeps writing; we map it to `ReconStatus` in the legacy-sync phase | Move the Statements API logic into PayOps now |
| 9 | Bank interface | Folders as today (paths in config), SFTP later | SFTP/host-to-host now |
| 10 | PDF attachments | QuestPDF (free Community licence if company revenue < USD 1M; otherwise paid) | CSV attachments, no PDF |
| 11 | Background jobs | One `PayOps.Worker` with config schedules + `JobLease` | Hangfire/Quartz (extra tables and dashboard) |

## 8. Out of scope for Phase 2 (Phase 3+)
UI. CoreWCF facade for existing WCF clients. Legacy data ETL / sync (incl. creating the 11 placeholder clients). Entra app
registrations and hosting the APIs on a server. Banks other than Nedbank. Billing invoices. Pastel export
(`sp_BankRuns_PastelFile_Create`). Audit log / user-to-client access limits.

## 9. Delivery
| PR | Content | Rough effort |
|---|---|---|
| 2b | ServiceDefaults + Batch File API + `BatchFileApi` migration | 1-2 days |
| 2c | Payments API + `PaymentsApi` migration | 1 day |
| 2d | Bank File API + `PayOps.Worker` bank jobs + `BankFileApi` migration (needs a sample legacy bank file + ACK/NACK/Unpaid samples) | 2 days |
| 2e | Notifications API + dispatcher + scheduled notifications + `NotificationsApi` migration (needs SMTP/SMS test settings) | 1-2 days |
After each merge: migration to Docker `pg-schema-test` and SQLEXPRESS from your PC, then a short demo of the endpoints.
