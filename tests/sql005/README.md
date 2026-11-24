# Isolated SQL acceptance

## EF activation runbook acceptance

From the repository root, only with explicit disposable-database authorization:

```powershell
dotnet run --project tests/sql005 --configuration Verification -- --authorize-disposable-ef-activation
```

This additional focused path tests the actual compiled API `--migrate` / `--check` commands in child processes against newly created, identity-pinned LocalDB 2019 catalogs. It covers an empty installation, rich SQL009 history, rejected incompatible baseline prerequisites, exact source catalog initialization, edited defaults/activation/ordering/dimensions, immutable orders/accepted quotes/corrections/files/notifications/views/tariffs, edited feature reruns, and real SQL → API → Next/browser publication/cache behavior. It requires an existing safe local frontend `.next` build and local Chrome/Playwright tools; it never builds or publishes them. See [activation guide](../../docs/CHAP-EF-MIGRATION-ACTIVATION-FA.md) and current acceptance report.

The normal application executable is rebuilt through the project reference. Child-process SQL/provider configuration uses only the guarded fixture and disabled SMS. Each activation run prints its own evidence directory under `.qa/ef-activation-<guid>`, including `acceptance-result.json`. The separate `--authorize-disposable-ef-modernization` suite exercises EF/catalog/magazine API regressions. The optional Express 2025 variants require a dedicated, empty local named instance with Shared Memory. See the startup and SQL 2025 acceptance guide.

## Current 008/009 correction and attention acceptance

With explicit task-specific authorization, run from the repository root:

```powershell
dotnet run --project tests/sql005/ChapGiti.SqlAcceptanceTests.csproj --configuration Verification -- --authorize-disposable-localdb-008-009
```

This path runs `OrderWorkflowAcceptance`, not the historical ticket-mutation scenarios below. It creates two separate new LocalDB 2019 instances/catalogs: an upgrade with synthetic 001–007 history through 008 then 009, and a fresh complete 001–009 chain. Both use the actual runner, checksums, application locks, transactions, Controllers and repositories. It also checks concurrent runner startup, rejected checksum drift in a disposable staged copy, and transactional rollback of a synthetic failing migration (never a shipped migration or registered checksum).

`OrderWorkflowFixtures` creates fake identities, historical orders/accepted quotes/tickets and private PDFs. SQL/API acceptance covers conditional initial PDFs, correction request/submission/review, multiple cycles and PDF versions, production gates in modern and legacy routes, updated quote acceptance, concurrency, rollback and command/notification deduplication. Notification and order-view tests verify independent read state, recipient isolation, new sessions, real WebSocket SignalR delivery/reconnection and authoritative HTTP resync. The current API requires an original PDF, accepts the latest replacement for a correction, preserves accepted prices and requires signed delivery terms. Closed historical tickets remain accessible to their owner and administrators. Invalid mutations and unauthorized downloads must be rejected. No real OTP/SMS/Push transport is constructed.

No server, catalog or connection-string override is accepted; `.env.local` is never read. The actual server identity, LocalDB property and catalog are pinned before every direct write/read, runner operation and HTTP/API operation using the repository's own scoped connection. Only synthetic order-intake limiter expiry is advanced between independent batches, after verifying the real rate limit remains enforced. Application rate limits are unchanged.

The final machine-readable report is `.qa/order-workflow-sql-2026-11-17/sql-result.json`. Dedicated databases and LocalDB instances are removed after identity revalidation; only each fixture's own synthetic private storage is removed. A test-looking database name alone never authorizes cleanup. Existing instances and working/deployment `ChapGiti` are never connected. Generic `scripts/sql-test-fixture.mjs` stays disabled.

See the Persian acceptance report for actual engine build, results, limitations and later activation requirements. This verifies an isolated test target; it does not activate the working database or verify another SQL Server version.

## Historical 005–007 harness (not current ticket behavior)

The older flag below is retained as historical infrastructure. It exercises the historical runtime before later EF migrations and is not an acceptance contract for the current application.

Run from the repository root **only with task-specific authorization for a disposable SQL database**:

```powershell
dotnet run --project tests/sql005/ChapGiti.SqlAcceptanceTests.csproj --configuration Verification -- --authorize-disposable-localdb-005
```

Without the explicit flag the executable exits with code 2 before any SQL operation. It is deliberately not part of ordinary test/startup/deploy scripts.

Prerequisites: .NET 10 and SQL Server 2019 LocalDB (`SqlLocalDB.exe`, version 15.0). The fixture creates its own uniquely named LocalDB instance and `ChapGiti_Acceptance005_<random>` database. It does not accept a server or connection string and does not read `.env.local`. Application configuration sources are cleared. Every seed/DDL operation and API request verifies the actual server and database; the API guard uses the same scoped `SqlStore` connection as its repositories. No code switches databases with `USE`.

The fixture applies source migrations with the actual migration runner, uses synthetic customer/admin/order/ticket records, and exercises the real controllers, authentication, transactions and SQL repositories. SMS/OTP transports are replaced with no-send implementations. Private storage points inside its own `.qa` directory.

Coverage includes 005 without 004 (transactional DDL probe rolled back), full 001–005 chain, checksums and idempotent runner, historical general tickets, ownership, messages, filters/pagination/statuses, note history/idempotency/Unicode boundaries/privacy, referential constraints and rollback.

The database is dropped only after revalidating the pinned server/name; the dedicated instance is then stopped and deleted. Synthetic result metadata and staged SQL remain under `.qa/ChapGiti_Acceptance005_*/result.json`. Cleanup failures are reported as failures, never hidden. Existing LocalDB instances, operational `ChapGiti`, and private customer files are not used or removed.

This is SQL Server integration coverage, not a production rollout or a guarantee about another engine version/collation. The operational instance inspected in the acceptance task was SQL Server 2022; this fixture runs SQL Server 2019 LocalDB.


The same positively guarded fixture also stages and tests 006 after the 001-005 legacy checks, including historical price preservation, quote revisions, atomic owner acceptance, command replay, real rollback and Browser/Next/API/SQL workflows. Operational ChapGiti is never a target; the explicit flag still applies.

It then stages **007_in_app_notifications.sql** and verifies recipient FKs/CHECKs/indexes, checksum/rerun, no historical backfill, safe event projections, opposite-party general/order/consultation support, deterministic pagination, read persistence, independent simultaneous sessions and real notification/business rollback. Admin notification and proposal ownership survives profile email changes. Only synthetic ticket-intake rate counters in this disposable fixture are expired between independent acceptance phases; production limits are unchanged.

Set `CHAP_SQL_BROWSER_AUDIT=true` for the guarded Browser → Next proxy → API → SQL journey, including the complete notification UI checks. Browser child output is captured in `.qa/notifications/sql-browser-process.log`; the final child results are `.qa/full-audit/sql-browser.json`. These paths contain only isolated test evidence, not release packages. All helpers still validate server/catalog before writes, use no-send transports and remove only their own database/instance/private fixtures.

## Packaged Windows runtime smoke test

Extract a freshly verified release under this checkout’s `.qa` directory and run the following from the source root. The harness guards a unique LocalDB catalog, runs the migration and health checks from the extracted Windows executable, initializes installation-specific frontend manifests and exercises the packaged standalone server on loopback. It checks public and magazine pages, customer and administrator logins, estimates, PDF order intake and tracking, initial ticket attachments, access isolation, WebSocket notifications and the persisted inbox. A final schema check and SQL record counts run before disposal.

```powershell
dotnet run --project tests/sql005 -c Verification -- --authorize-disposable-release-smoke ".qa/ticket-release/extracted"
```
