# PayOps Phase 2c: Payments API (plan for approval)

Nothing is built until you approve. Delivered as one PR with CI green; after you merge, I back up both PayOps
databases and apply the migration from your PC, like the earlier steps.

## 1. Purpose
The ops back office for batches that are already in PayOps (2,213 legacy batches + new uploads): find a batch, see what
happened to it, reconcile (funding), put it on hold, see what was paid, and handle rejection refunds. It replaces the legacy
Payment Admin screens. It does **not** create bank files (that is 2d, Bank File API) or send emails/SMS (2e).

## 2. Projects (same pattern as Client and Batch File APIs)
- `src/Services/Payments/PayOps.Payments.Data` (exists, read-only queries today) - extended with commands and services.
  Own `PaymentsDbContext` mapping only the tables it needs; LINQ only, no stored procedures.
- New `src/Services/Payments/PayOps.Payments.Api` - Minimal APIs, `PayOps.ServiceDefaults` (Entra auth, ProblemDetails,
  OpenAPI, Swagger UI in Development, `/health`), Dockerfile. Port **5190** in Development.
- Tests in `tests/PayOps.Tests` (Testcontainers SQL Server 2022 + WebApplicationFactory, `PaymentsApiMarker`).

## 3. Who can do what
| Role | Can |
|---|---|
| `PayOps.Reader` | all GET endpoints and CSV exports |
| `PayOps.Operator` | + reconcile, hold/release, comments, confirm/edit rejection refunds |
| Client API keys | nothing here (clients already read their own batches through the Batch File API) |

Development: sign-in switched off, every caller is an operator (as in the other APIs).

**Who did it:** every change records the staff user. On a staff user's first call the API creates their `AppUser` row from
the Entra token (object id -> `ExternalId`, name, email). In Development a fixed `dev.user` is used.

## 4. Endpoints (all under `/api`, paged, filters optional)
### 4.1 Batches
| Endpoint | What |
|---|---|
| `GET /batches` | search: client (unique ID or name), primary client, client reference, unique reference, service type, payment date from/to, recon status, on hold, in a bank run yes/no, has rejections |
| `GET /batches/{id}` | header, client, totals, fee, recon status, hold, bank run, counts per transaction status |
| `GET /batches/{id}/payments` | the payment records, filter by status, search by employee code / surname / account |
| `GET /batches/{id}/comments` / `POST` | read and add comments (`PaymentSummaryComment`) |
| `GET /batches/{id}/history` | who changed what and when, from the temporal `PaymentSummaryHistory` + comments |
| `GET /payments/{id}` | one payment with its status history (`PaymentRecordStatusHistory`) and rejection |

### 4.2 Reconciliation (funding)
| Endpoint | What |
|---|---|
| `POST /batches/{id}/reconciliation` | set Reconciled / Unreconciled / Discrepancies with a mandatory comment (saved as a batch comment) |
| `GET /reconciliation/unreconciled` | batches not yet reconciled and not in a bank run, oldest payment date first |

Rule: the recon status can't change once the batch is in a bank run.

### 4.3 Holds
| Endpoint | What |
|---|---|
| `POST /batches/{id}/hold` | put on hold with a reason; only while the batch is not in a bank run |
| `POST /batches/{id}/release` | release with a comment |

A held batch is never picked by the bank run (enforced in 2d by the same flag).

### 4.4 Paid Away and History (your definition: status Success)
| Endpoint | What |
|---|---|
| `GET /paid-away` | payments that became Success today (SAST day) |
| `GET /paid-history?from=&to=` | same for a date range (max 31 days per call) |
Both can be filtered by client and service type and show totals.

### 4.5 Rejections and refunds
| Endpoint | What |
|---|---|
| `GET /rejections?status=` | PendingUnreconciled (new, waiting for ops), Pending (confirmed, waiting for the next bank run), Transmitted, Success, Rejected |
| `GET /rejections/{id}` | detail incl. original payment and batch |
| `PUT /rejections/{id}/refund-account` | correct the refund account name/branch/account (only while PendingUnreconciled) |
| `POST /rejections/{id}/confirm` | PendingUnreconciled -> Pending, with comment; written to the status history |

### 4.6 CSV export
Every list above also answers `Accept: text/csv` (or `?format=csv`), streamed, same filters, no paging limit.

## 5. Migration `PaymentsApi` (one new file; earlier migrations untouched)
- `PaymentSummary`: `IsOnHold bit NOT NULL DEFAULT 0`, `HoldReason nvarchar(500) NULL`, `HeldByUserId int NULL` (FK AppUser),
  `HeldDate datetime2 NULL`, `ReconStatusChangedByUserId int NULL`, `ReconStatusChangedDate datetime2 NULL`,
  `RowVersion rowversion` (two people editing the same batch -> 409 instead of a silent overwrite).
- `PaymentRejection`: `ConfirmedByUserId int NULL`, `ConfirmedDate datetime2 NULL`, `RowVersion rowversion`.
- Indexes for the search: `PaymentSummary (PaymentDate)`, `(ReconStatusId, PaymentBatchId)`, `(IsOnHold)` filtered;
  `PaymentRecordStatusHistory (TransactionStatusId, ChangedDate)`; `PaymentRejection (TransactionStatusId)`.
- Idempotent script tested twice on a copy of your imported SQLEXPRESS data (2,213 batches) before the PR, so the
  `EXEC` issue from #7 can't repeat.

## 6. Out of scope for 2c
Bank runs, bank files and bank responses (2d). Sending notifications (2e; 2c only writes outbox rows if you choose so, see
decision 6). Auto-reconciliation from your Statements API (decision 5). UI. Excel exports.

## 7. Tests
Search filters and paging; recon/hold rules (blocked in a bank run, comment required, 409 on concurrent edit); rejection
flow and history rows; Paid Away/History day boundaries in SAST; CSV output; role checks (Reader can't write, API key
refused); AppUser auto-create; migration on a DB with existing batches.

## 8. Effort
About 1-2 days of work, one PR.
