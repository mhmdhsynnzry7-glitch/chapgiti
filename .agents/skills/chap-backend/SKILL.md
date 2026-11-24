---
name: chap-backend
description: Implement or review CHAP GITI ASP.NET Core controllers, application use cases, domain logic, customer/admin authentication, ordering, pricing, ticket and API contracts. Use for any backend feature or API change.
---

# CHAP Backend Workflow

1. Read repository `AGENTS.md`, backend README, architecture docs, affected C# classes, endpoint consumers, and existing tests before editing. Repository code is authoritative; this skill does not assert features are already implemented.
2. Describe the impacted user journey, API contract, business invariant, and existing test coverage in a short plan.
3. Preserve `.NET 10` and thin ASP.NET Core API Controllers: Application depends on Domain; Infrastructure implements Application abstractions; Domain never depends on either. Do not bypass application services from controllers or move business rules into Next.js.
4. Define explicit request/response DTOs and validation; preserve existing routes and status codes unless the task requests change. No parallel controller/dispatcher route conflicts. Use typed errors and meaningful HTTP outcomes.
5. For printing, inspect `PrintPricing.cs`, frontend `book-pricing.ts`, tariff defaults, and quote/order consumers together. Backend owns authoritative pricing. Keep historical quote details in storage even when hiding breakdown in customer UI.
6. For orders and tickets, preserve state transitions, ownership, idempotency, optimistic concurrency and audit behavior. For uploads, retain PDF constraints and private storage.
   Admin order lists default to all statuses and sort by descending date/ID with matching filtered counts. Customer details expose only owned specifications and safe file metadata. All existing finishing rates retain their units/formulas; validate tariff saves without touching historical snapshots. Directory last-login data comes from stored customer sessions, never account creation or refresh rotation.
7. For identity, customer login is mobile+password; admin login is a **separate** session using fixed username `admin`+password. Keep cookie/refresh scopes isolated. Admin can update their own email and password; cannot edit other customers' personal profiles. Verify behavior against current source.
8. Registration target: mobile → MelliPayamak Console OTP verification → personal profile/password → account. OTP provider generates the code. Avoid changing provider transport without evidence.
9. Global **new/changed** password policy: minimum 6 characters, >=1 ASCII uppercase A–Z, >=1 special character; show same Persian guidance in all password forms. Previously issued passwords continue to work for login.
10. Add focused unit/API tests for success, failure, authorization boundaries, regressions and any error mapping changes. Use mock provider calls; never send live SMS from test suites.
11. Report files/routes changed, tests actually run, migration impact and unresolved limitations. **No publish, packaging, server modifications or live database writes without explicit permission.**

## Acceptance checks
- Order workflow uses Domain `OrderWorkflow`; POST `/api/admin/orders/{id}/open` starts review once, GET stays read-only. New requests require a versioned proposal, explicit owner acceptance into confirmed, then explicit administrator start; direct approval is legacy-only. Rejection needs a reason and uses terminal cancelled. Persisted printOnly controls binding, legacy confirmed stays actionable. Append private notes idempotently and keep old note text. General and order-linked ticket intake, replies and administrator status changes are active. Authorize each ticket and its private attachments. Activity projects persisted evidence only; no invented logout or SMS-log relabeling.
- Application/Domain stay independent from HTTP, SQL and concrete provider clients.
- Existing customer order/ticket relationships and administrator login survive.
- Relevant `test:auth`, architecture tests and type contracts run when available.

## Registration acceptance contract

- The user journey is `mobile → Console OTP → server-validated verification grant → profile/password → account persistence`. Do not create a completed customer account on OTP issuance.
- Bind verified grants to the normalized mobile number and registration purpose, validate them on the completion endpoint and consume atomically. Existing mobiles go to password login.
- The current API uses `otp/request` (phone/purpose only), `otp/verify` (issues a five-minute registration grant), then `registration/complete` (grant plus profile/password). Grants store only a purpose/mode-bound token digest in the existing OTP repository, never authenticate a session, and are consumed in the account-creation transaction. Preserve expiry, replay, duplicate and mode-change checks without operational migrations.
- Logical `grant` is not an allowed SQL delivery status. Infrastructure encodes it as register/pending/five reserved attempts/no OTP hash and maps it back at the repository boundary. Respect the deployed CHECK constraints; use the schema-aware repository/API regressions instead of relying only on an unconstrained memory store.
- Keep admin `admin` login and cookies fully separate from customer authentication.
- Recovery checks normalized mobile, a completed account/profile and verified ownership before creating a challenge or calling SMS. Recheck eligibility before recovery verification and invalidate stale state. Unknown accounts use `CUSTOMER_ACCOUNT_NOT_FOUND`; distinguish incorrect/expired/missing/consumed/exhausted OTP and provider failures with typed codes. The former neutral recovery send policy is superseded; preserve legacy unverified-phone restrictions.

## CMS V2

- Services use the existing Settings.services JSON key with locked serializable writes, stable IDs, canonical slug aliases and optimistic versions. Keep marketing content independent of printing/pricing identifiers; no automatic operational writes. Consultation uses configured `/contact` information; never recreate a support conversation as its destination.
- Optional banner CTA is a validated label/link pair. Social settings use exact HTTPS platform hosts. Magazine is restored with published-only SQL-backed public/admin routes; portfolio remains retired and its promotions are filtered at read time.

## Onion/EF and catalog/magazine modernization

EF Core 10.x is the primary Infrastructure implementation. Keep Domain/Application free of EF/SQL/HTTP, map the actual legacy schema with Fluent API and preserve shared UoW, locks, concurrency, idempotency and post-commit hints. Document each remaining SQL path. Validate the SQL001–009 schema before an empty EF baseline and use EF migrations for newer schema, never startup/EnsureCreated. Catalog names are presentation; IDs/codes and supported pricing rules are stable, with historical snapshots preserved. Articles reuse existing tables/routes/assets with explicit draft/published/archived rules, safe content, private drafts, optimistic versions, stable/aliased slugs and immutable recorded publication dates. Preserve active ticket mutations, and keep the retired portfolio and article-footer calculator CTA out of new work. Permission to apply a Development migration is independent of release, deployment and Production access.


## Request-to-production acceptance

New requests use OrderQuoteService and Domain quote/transition rules. Never set final_price or start printing when sending a proposal. Actor/payload-bound command receipts and expected versions protect mutations; quote acceptance and order price/status update in one transaction.

## Notification acceptance

Publish channel-independent business notifications inside that same UoW, only after meaningful validated changes. Keep recipient-scoped customer/admin APIs, immutable administrator keys, safe template text/targets, idempotent read states and repository deduplication. Notification failure must roll back a schema-enabled business transaction. Preserve explicit pre-migration availability and do not add push/SMS transports.

## Current correction and attention policy

- `OrderCorrectionService`: request → customer submission → administrator review. The explicit per-cycle PDF requirement is immutable to the customer. A required replacement must be a newly linked valid PDF from that order/cycle; prior file IDs cannot satisfy it. Rejection requires a reason, terminal cycles/history remain immutable, and cancelled/completed orders cannot be reopened by review.
- `OrderProductionReadiness` runs inside the order lock/UoW for both explicit production start and historical PATCH-to-print. Outstanding/rejected corrections, missing/invalid required PDFs or an unaccepted latest quote block printing. Use the latest approved file reference, preserve earlier accepted quote versions, and require fresh acceptance when issuing a changed quote.
- Validate the primary private PDF and explicit customer consent on every new print request. Production requires approval of the file, quote and final delivery conditions.
- Order badges use independent persisted `Order_Views` and the recipient's order-notification sequence, never notification `read_at`. GET, lists, navigation and read-all do not mark an order viewed. Capture the observed sequence before the detail snapshot; acknowledgements advance monotonically without consuming a newer event. Reuse the role-isolated notification summary/SignalR/polling infrastructure.
- Ticket writes use the existing business transaction and scoped post-commit SignalR hub. A conversation cannot accept a quote, file or delivery terms.

Follow the current workflow and support rules in root AGENTS.md. Automated tests use isolated data and mocked delivery. Apply reviewed Development migrations only through the guarded project runner after target verification and a verified backup. Publish editorial content in Development only when the owner authorizes it through the existing administrator API.

- Final terms use an explicit revision of the active agreement, the locked order version and idempotency key. Authorize attachment downloads by ticket. Compensate staged private writes on rollback. Return Persian errors, stable codes and trace IDs.
