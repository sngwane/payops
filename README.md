# PayOps

Payment operations suite: client management, batch intake, payment processing, bank files, rejections/refunds,
reconciliation, notifications and billing. Successor to the legacy BulkPayments platform (migrated capability by capability).

## Stack
.NET 10, EF Core 10 (LINQ, code-first migrations), SQL Server 2022, xUnit + Testcontainers.

## Layout
```
src/Shared/PayOps.Database                    Entities, full model (PayOpsDbContext), EF migrations - owns the schema
src/Services/Client/PayOps.Client.Data        Client API context + LINQ commands/queries (clients, bank accounts, API keys)
src/Services/Client/PayOps.Client.Api         Client API (ASP.NET Core minimal API, Entra ID auth)
src/Services/BatchFile/PayOps.BatchFile.Data  Batch File context, legacy XML reader, batch rules, submission service
src/Services/BatchFile/PayOps.BatchFile.Api   Batch File API (client X-Api-Key uploads + staff endpoints)
src/Shared/PayOps.ServiceDefaults             Shared Entra auth, roles, ProblemDetails, OpenAPI, /health
src/Services/Payments/PayOps.Payments.Data    Payments API context (subset of tables) + read-only LINQ queries
tests/PayOps.Tests                            Integration tests against SQL Server 2022 in Docker
deploy/docker-compose.yml                     SQL Server container for local / single-server use
```
Each API gets its own small DbContext that maps only the tables it uses. Only `PayOpsDbContext` has migrations.

## Database
- One database, `PayOps`, single `dbo` schema. Tables are PascalCase singular (`PaymentBatch`, `PaymentRecord`).
- Client hierarchy: `PrimaryClient` 1..n `SecondaryClient` 1..n `PaymentSummary` (client batch) 1..n `PaymentRecord`.
  Batches always belong to a secondary client.
- `PaymentBatch` is a bank run, with maker-checker approval (`ApprovedByUserId` must differ from `CreatedByUserId`).
- Rejections: a bank-rejected `PaymentRecord` gets a `PaymentRejection` refund to the client's rejection account,
  starting at `PendingUnreconciled` (5) -> `Pending` (1) -> `Transmitted` (2) -> `Success` (3) / `Rejected` (4).
  Status changes are recorded in `PaymentRecordStatusHistory`.
- `PaymentSummary` is a system-versioned temporal table (history in `PaymentSummaryHistory`).
- `UniqueClientId` is unique across primary and secondary clients and can't change after creation.
  `SecondaryClient.RequiresReview` marks placeholders created for unmatched legacy batches (under the inactive
  `LEGACY-UNMATCHED` primary); only those can be moved to another primary.
- `ClientBankAccount`: a primary client's Nominated / Charges accounts, at most one active per type.
- `ClientApiKey`: belongs to exactly one primary or secondary client; only the SHA-256 hash is stored, at most 2 active.
- Client tables use `rowversion` for optimistic concurrency (sent back as `rowVersion` on updates).
- `BatchUpload` logs every upload attempt (errors and SHA-256 payload hash, never the payload). `BatchContact` holds the
  contacts sent with a batch. `PaymentSummary.FeeTotal` holds billing fees (from `BillingRate`) when the footer includes them.
- `NotificationMessage` is the outbox: rows written in the same transaction as the change, sent later by Notifications.
  `JobLease` stops two worker instances running the same job. `PublicHoliday` feeds the payment-date checks.
- Users authenticate with Entra ID; `AppUser.ExternalId` holds the Entra object id. No passwords are stored.

## Getting started
```bash
dotnet tool restore
dotnet build
dotnet test            # needs Docker; starts SQL Server 2022 automatically
```

## Client API
```bash
dotnet user-secrets set ConnectionStrings:PayOps "<connection string>" -p src/Services/Client/PayOps.Client.Api
dotnet run --project src/Services/Client/PayOps.Client.Api   # http://localhost:5101, OpenAPI at /openapi/v1.json
```
- Development runs without sign-in (every caller is a client admin). Everywhere else set `Authentication:TenantId`
  and `Authentication:Audience` for the Entra app registration; tokens need the `PayOps.ClientAdmin` (read/write)
  or `PayOps.Reader` (read-only) app role.
- Routes: `/api/primary-clients`, `/api/secondary-clients`, `.../bank-accounts`, `.../api-keys`, `/health` (anonymous).
  Lists take `search`, `isActive`, `page`, `pageSize` (default 50, max 200). Errors are RFC 9457 problem details.
- A new API key is returned once, in the create response; it can't be retrieved again.
- Container: `docker build -f src/Services/Client/PayOps.Client.Api/Dockerfile -t payops-client-api .`

## Batch File API
```bash
dotnet user-secrets set ConnectionStrings:PayOps "<connection string>" -p src/Services/BatchFile/PayOps.BatchFile.Api
dotnet run --project src/Services/BatchFile/PayOps.BatchFile.Api   # http://localhost:5180
```
- Clients send their API key (from the Client API) in `X-Api-Key`. A primary's key may submit for any of its secondaries.
  - `POST /api/batches` (JSON), `POST /api/batches/xml` (legacy `BulkPaymentXml`, checked against the legacy XSD),
    `POST /api/batches/validate` (either format, nothing stored), `GET /api/batches`, `GET /api/batches/{id}`, `GET /api/batches/{id}/rejections`.
  - Errors are per field (`payments[12].accountNumber`). Resending an accepted batch returns 409 with `batchId`.
- Staff (Entra): `GET /api/uploads` and `GET /api/public-holidays`, `/api/billing-rates` need `PayOps.Reader`, `PayOps.Operator`
  or `PayOps.ClientAdmin`; changing holidays and rates needs `PayOps.Operator`.
- Rules (legacy plus TwoDay): footer records must match; footer total is the payment sum or the sum plus fees; SDV/RTL/DW
  not in the past, OneDay after today, TwoDay at least two business days ahead; never a Sunday or public holiday.
  Dates use `BatchFile:TimeZone` (Africa/Johannesburg). Limits: `BatchFile:MaxRecords` (50,000), `BatchFile:MaxRequestBytes` (10 MB).
  Allowed service types: `BatchFile:AllowedServiceTypes` (SDV, OneDay, TwoDay).
- Container: `docker build -f src/Services/BatchFile/PayOps.BatchFile.Api/Dockerfile -t payops-batchfile-api .`

## Legacy data import

`tools/PayOps.LegacyImport` copies the legacy BulkPayments data into PayOps: clients, bank accounts, access keys (stored as SHA-256 hashes only), bank runs, batches, payments, rejections, comments, public holidays, billing rates and bank profiles. It only reads legacy. Imported rows keep their legacy key in `LegacyId`, so it can be re-run until cut-over; rows already copied are updated, not duplicated.

```bash
# 1. read-only profile of the legacy data
sqlcmd -C -I -b -S <server> -d BulkPayments -i deploy/legacy-import/profile-legacy.sql
# 2. connection strings come from the environment, never the command line
export LEGACY_CONNECTION_STRING="Server=...;Database=BulkPayments;..."
export PAYOPS_CONNECTION_STRING="Server=...;Database=PayOps;..."
# 3. dry run: full import, report written, everything rolled back
dotnet run --project tools/PayOps.LegacyImport -- --dry-run
# 4. real run (back up PayOps first)
dotnet run --project tools/PayOps.LegacyImport
```

Each run writes `report.md`, `issues.csv` (every skipped or repaired row and why), `batch-differences.csv` and `clients.csv` (legacy vs PayOps counts and amounts). Exit code 0 means every count and total matches.

Bad legacy rows:
- A missing required text value is stored as `(missing)`.
- Over-long text is cut to fit.
- Duplicate client IDs and broken links are skipped.
- A batch with no matching secondary client gets an inactive placeholder under `LEGACY-UNMATCHED`, flagged `RequiresReview`.
- Empty or duplicate references and sequence numbers become `LEGACY-<HeaderId>` and `-<HeaderId>`.

All of these appear in `issues.csv`. Billing rates: only the latest rate per service type is imported. Client notification contacts (`NotificationXml`) wait for the Notifications API.

## Migrations
Connection strings are never committed. The design-time tools read `PAYOPS_CONNECTION`.
```bash
# add a migration after changing entities/configurations (always a new file)
dotnet ef migrations add <Name> -p src/Shared/PayOps.Database

# apply to a server
PAYOPS_CONNECTION="Server=127.0.0.1,14333;Database=PayOps;User Id=sa;Password=...;TrustServerCertificate=true" \
  dotnet ef database update -p src/Shared/PayOps.Database

# SQL for DBA review (safe to run more than once)
dotnet ef migrations script --idempotent -p src/Shared/PayOps.Database -o artifacts/PayOps.sql
# run it with QUOTED_IDENTIFIER on (needed for the filtered index): sqlcmd -I -b -S <server> -d PayOps -i artifacts/PayOps.sql

# self-contained executable to migrate any server (e.g. after moving to a new host)
dotnet ef migrations bundle --self-contained -r win-x64 -p src/Shared/PayOps.Database -o artifacts/payops-migrate.exe
artifacts/payops-migrate.exe --connection "<connection string>"
```
