# PayOps plans

The plans written and approved while modernising BulkPayments into PayOps, kept as they were approved. The code, migrations
and the root `README.md` are the source of truth when they differ. Early documents use the working name **PaymentGateway**,
which became **PayOps**.

| Folder | Document | What it covers | Status |
|---|---|---|---|
| `00-discovery` | [BulkPayments-Modernization-Plan](00-discovery/BulkPayments-Modernization-Plan.md) | Analysis of the legacy BulkPayments code and the overall .NET modernisation plan | Background |
| | [BulkPayments-Target-Service-Design](00-discovery/BulkPayments-Target-Service-Design.md) | Service boundaries (Client, Batch File, Payments, Notifications, Bank File), data model and API design | Approved direction |
| | [BulkPayments-Data-and-Intake-Upgrade-Playbook](00-discovery/BulkPayments-Data-and-Intake-Upgrade-Playbook.md) | Database-first upgrade and client intake via CoreWCF | Background |
| `01-phase1-database` | [Step1-Plan-NewDatabase-and-LegacySync](01-phase1-database/Step1-Plan-NewDatabase-and-LegacySync.md) | Phase 1: new database and sync from legacy | Approved, done (PR #1) |
| | [PaymentGateway-Database-Schema-v1-Draft](01-phase1-database/PaymentGateway-Database-Schema-v1-Draft.md) | First schema draft | Superseded |
| | [PaymentGateway-schema-plan](01-phase1-database/PaymentGateway-schema-plan.md) | Corrected schema: one shared `dbo` schema | Approved, done |
| | [PaymentGateway-Legacy-Mapping-Plan](01-phase1-database/PaymentGateway-Legacy-Mapping-Plan.md) | Legacy table to new table mapping | Approved |
| `02-phase2-apis` | [PayOps-Phase2-Plan](02-phase2-apis/PayOps-Phase2-Plan.md) | Phase 2: the five APIs, worker, outbox, decisions | Approved |
| | [PayOps-Phase2-Client-API-Plan](02-phase2-apis/PayOps-Phase2-Client-API-Plan.md) | 2a Client API | Done (PR #2) |
| | [PayOps-Phase2c-Payments-API-Plan](02-phase2-apis/PayOps-Phase2c-Payments-API-Plan.md) | 2c Payments API | Done (PR #9) |
| `03-legacy-import` | [PayOps-Legacy-Data-Copy-Plan](03-legacy-import/PayOps-Legacy-Data-Copy-Plan.md) | Copying legacy BulkPayments data into PayOps | Done (PR #6, #7, #8) |

The Batch File API (2b) was built from section 3 of the Phase 2 plan (PR #3, #4). Next: Bank File API (2d), then Notifications (2e).
