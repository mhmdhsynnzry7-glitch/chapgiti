# CHAP GITI — Implement Request-to-Quote-to-Production + In-App Notifications (Codex)

> Current owner override (2026-11-16): follow `ORDER-CORRECTIONS-AND-ATTENTION-FA.md` and the current AGENTS.md before the historical phases below. Implement reviewed correction cycles and independent order attention; active support intake/replies/status writes and support realtime are retired, with authorized read-only history. Consultation links go to configured contact information. New source-only migrations are 008 then 009; no operational database access or migration execution is authorized by this implementation task. Older requirements for creating support conversations or automatically clearing information requests are superseded.

## Mission
You are the lead Full-Stack Product Engineer and Solution Architect for the existing CHAP GITI repository.

**Implement** the newly approved business model and the persistent in-site Notification Center for both customer and admin: customer login and profile completion → cut-specific estimated pricing → print request → administrator review → versioned quote proposal → explicit customer approval → queue for production → administrator starts printing → optional binding → ready → delivered.

This request supersedes the old rule that administrator price approval immediately moves new orders to `printing`.

Use the project skills `$chap-backend`, `$chap-frontend`, `$chap-testing`, `$chap-database`, and follow root `AGENTS.md`. Read `docs/ARCHITECTURE.md`, current handover notes and the recent workflow-support report. The companion document `PRODUCT-ARCHITECTURE-FA.md` (v3, including section 7 on notifications) is the authoritative product/architecture specification for this task.

**Do not deploy, publish, package, execute real SMS, delete operational data or apply migrations to the operational ChapGiti database.**

## Phase 1 — Verify repository reality and create an implementation map

1. Inspect current Next.js calculator, order submission, customer orders/details, admin order details, ticket workflows and authentication boundaries.
2. Inspect Domain `OrderWorkflow`, existing `confirmed`/`printing` behavior, `AdminOrderService`, Order repositories, Controllers, price fields, order events and SQL migrations (including actual applied state of 004/005 and any later migrations); inspect existing notification, event/audit and message infrastructure before adding new tables.
3. Identify the current API contracts and database constraints. Check that the calculator and `/api/quote` both require a customer with a completed profile.
4. Audit the difference between the most recent `AGENTS.md` and the newly approved flow. The latest explicit user decision overrides old order-approval behavior; preserve unrelated rules.
5. Produce a short plan and migration compatibility matrix, then implement authorized source changes. Do not stop after the plan.

## Phase 2 — Domain state machine and versioned quote

Maintain existing statuses and introduce the smallest status needed: `awaiting_customer_approval` (or a repository-compatible equivalent, clearly documented).

Target transitions for new orders:

- `new -> review`: explicit administrator opening, exactly once; GET/prefetch remains read-only.
- `review -> awaiting_customer_approval`: admin sends proposal with valid amount and clear terms.
- `review -> cancelled`: admin rejects with reason.
- `awaiting_customer_approval -> confirmed`: customer accepts the **latest active quote revision**.
- `awaiting_customer_approval -> review`: customer requests revision with a reason; old proposal remains historical and is no longer acceptable.
- `awaiting_customer_approval -> cancelled`: customer declines with clear reason/actor attribution.
- `confirmed -> printing`: administrator explicitly begins actual printing.
- `printing -> binding OR ready`: based on persisted only-print/binding specification.
- `binding -> ready -> completed`: administrator records progress.

`cancelled` and `completed` remain terminal. Preserve historical `confirmed` and already-in-production records without inventing customer-approval events.

Introduce immutable revisions of an admin quote: unique quote ID, order ID, revision, amount in **toman bigint**, scope and exclusions, delivery terms if present, sent time, actor, revision state, accepted version/actor/time and idempotency/concurrency controls. Do not invent actual commercial prices, deadlines or shipping terms. Quote expiration can be optional; do not silently impose an arbitrary expiry.

Critical invariant: `Orders.final_price` is set as the accepted final price **only when the customer successfully accepts the quote**, atomically with status transition and quote acceptance. Existing historical final prices must be preserved. Previously sent quote versions must never be overwritten, or accepted once superseded.

Protect against double clicks, stale quote acceptance, simultaneous admin edits, inconsistent order versions and partial transactions.

## Phase 3 — Backend implementation

Keep ASP.NET Core .NET 10, Clean Architecture, Controllers, ADO.NET/SqlClient, SQL Server. Keep controllers thin, business constraints in Domain/Application and persistence in Infrastructure.

- Reuse existing `POST /api/quote`, `POST /api/orders`, `GET /api/customer/orders` and order-detail routes wherever possible; preserve old JSON contracts unless a coordinated additive extension is necessary.
- Add thin endpoints or matching routes for admin send/reissue quote, customer quote accept/request-revision/decline and admin start-printing.
- Verify user identity and order ownership using backend authentication. Administrator login/session remain independent, username `admin`.
- Keep existing order event audit trail and internal operational notes private.
- Do not couple transactions to SMS availability; record in-app status first, send notifications via existing safe integration only when enabled and authorized.
- For incomplete information or missing artwork, let admin request additional files/details while order remains in review; show a required-customer-action state without unnecessary new permanent statuses.
- Preserve PDF upload validation, ownership and file access safeguards.

## Phase 4 — SQL migration preparation and isolated testing

Check actual migration inventory first. Allocate the next unused migration number; do not assume `006` if later migrations exist.

Prefer a minimal additive `Order_Quotes` history table plus indexes/FK/constraints, adjusting other schema only as truly necessary. Use SQL Server-compatible data types, `nvarchar(max)` with suitable checked lengths where values exceed `nvarchar(4000)`, correct concurrency and transaction patterns.

Do not modify already-applied migrations, delete existing records or rewrite current prices. Test new migrations only on a positively verified disposable SQL Server test database. Include pre-migration historical orders and migration checksum/re-execution tests. If isolated SQL is unavailable, report BLOCKED rather than writing to `ChapGiti`.

## Phase 5 — Public frontend and customer account

1. Require customer sign-in and a completed profile for **price estimation**. Preserve the internal return path, server-side calculation and rate limits.
2. From estimator, primary CTA is «ارسال درخواست چاپ». Login/signup via existing mobile → MelliPayamak OTP → profile → account creation, or mobile/password for existing users. Preserve unfinished, non-sensitive request inputs across authentication safely; do not put sensitive data in URLs.
3. Keep the request form concise. Use the authenticated customer identity for contact information. Allow a file-free estimate and require a private PDF for order submission and a new PDF for a correction.
4. Confirmation text must say **request submitted for review**, not order confirmed or printing started.
5. Customer `/account/orders` becomes the single place to see all requests; do not require tracking code/mobile again. Preserve current routes and pagination contracts.
6. Add groupings: under review, awaiting your decision, accepted/queued, printing/binding, ready, delivered, rejected/cancelled.
7. Customer details show estimate, current proposal revision (if any), amount/terms, countdown only if explicit valid-until exists, progress, public events, permitted attachments and actions: «تأیید پیشنهاد»، «درخواست اصلاح»، «انصراف».
8. Use accessible Persian RTL, responsive layouts, appropriate loading/empty/error/success states and `200,000 تومان` formatting.
9. Do not leak private admin notes, other customers' orders or provider credentials.

Pages for services remain **consultation-only**: do not reintroduce print calculation/order CTAs there. Maintain any previously approved hiding of magazine/portfolio/footer admin link.

## Phase 6 — Administrator panel, support and tracking

1. Admin list distinguishes **new**, **review**, **awaiting customer approval**, **confirmed/queued**, **in production**, **ready** and completed/cancelled.
2. Existing explicit open action remains idempotent. Replace old direct price-approval-to-printing action with «ارسال پیشنهاد قیمت».
3. Admin can view quote revisions, customer decisions, request details/files and private notes.
4. A separate «شروع چاپ» action appears only for customer-approved `confirmed` new orders. Historical confirmed orders remain safely manageable.
5. On request-more-info or request-revision, show clear next steps, without creating duplicate requests.
6. Support stays **one helpdesk**: general and order-linked tickets, messages, states and customer/order ownership. Add «گفتگو درباره این درخواست» in order details without extra mobile/tracking-code prompt.
7. Quote revision is a structured action in the order Domain, not just a free-form support message. Support messages can accompany it but may not independently change order status.
8. Use existing order event feed for customer-visible tracking. Customer status notifications should be in-app first; optional SMS is complementary and must not gate order transactions.
9. Contact page remains business contact/social info; do not revive duplicate support forms.

## Phase 6A — Persistent in-app Notification Center (mandatory in this release)

Read section 7 of `PRODUCT-ARCHITECTURE-FA.md` and implement the complete Notification Center rather than placeholder notifications.

**Product behavior**

1. Show a bell icon, persistent unread badge, short dropdown and paginated full notification list in both the customer account and the separate admin panel. Respect the existing RTL visual system, responsive layouts, keyboard navigation and accessible announce/focus behavior.
2. Support notification read, mark-all-read, open internal target, creation timestamp and clear empty/loading/error states. Read state must survive refresh, re-login and multiple tabs.
3. Notify admin on a newly committed print request, customer actions on quotes (accept, decline, request revision) and new consultation requests. Notify customer on newly sent/revised quotes, requests for information, admin rejection, production status changes and completion. Notify the **other participant** when a new support message is committed. Do not create a new notification for the same acting user unless a distinct product need exists.
4. Only meaningful business events create notifications. A user viewing a page, pressing a noncommitting button or polling a list does not.
5. Start with lightweight polling approximately every 20 seconds, refresh on window focus and after relevant successful actions; pause/decrease in hidden tabs. If there is an already working reliable real-time transport, reuse it when compatible. Do not add SignalR/WebSockets merely for novelty. No phone push in this scope.

**Backend and persistence**

6. Inspect all current domain events, ticket messages, OrderService flows and SQL repositories. Reuse event/audit abstractions when suitable. Add an Application notification use case and repository abstraction, thin controllers, SQL persistence, a migration with the next available number, and indexes suitable for recipient+read+date queries.
7. Store `recipient kind/customer-or-admin`, the authenticated recipient ID, event type, entity reference, safe text, internal target path, creation time, optional read time and a unique deduplication key. Avoid arbitrary external redirect links and avoid storing/returning secrets or private admin note content.
8. Persist notifications only for **successful committed** operations, preferably in the same SQL transaction/UoW. Failed, rolled-back, rejected and idempotently retried business actions must not create phantom or duplicate notifications. If a specific workflow cannot share a transaction, document and implement the smallest reliable after-commit/outbox mechanism already supported by the repo. Do not introduce a broker or an external notification service.
9. Make notification APIs distinct for admin and customer to preserve independent sessions. Example: `/api/customer/notifications` and `/api/admin/notifications`, plus unread-count, read-one and read-all operations. Use the authenticated principal as recipient, never a user ID passed by the client. Enforce ownership and anti-cross-context access in controllers and application/repositories.
10. Ensure notification delivery does **not** execute the business transition a second time. An unread flag never determines whether an order can advance or a quote may be accepted.

**Future delivery channels**

11. Separate domain event generation from the in-app rendering channel so that browser/Web Push or mobile push can be added in a later explicit phase. No Firebase/FCM/APNs setup, push permission prompt, device registration, Service Worker, SMS sends or real push notification is authorized here.

**Mandatory testing**

12. Cover admin order-new, admin quote action, customer quote/new reply, general and order-linked support, statuses, owner isolation, retry/dedup, transaction rollback, unread badge, open/read/read-all and multi-session persistence. Test consumer data isolation across the distinct admin/customer authentication contexts. Use isolated SQL tests for writes and browser tests for polling/read UI; never modify operational `ChapGiti`.
13. Report the migration's exact filename/number and whether it was only prepared/tested or separately authorized to run. Do not mark the Notification Center fully operational against the actual database if its migration is pending.

## Phase 7 — Automated tests and acceptance

Use `$chap-testing`. Add negative-path and regression tests (unit, API, browser, isolated SQL) including:

- Guest and administrator-only sessions cannot estimate. A signed-in customer with a completed profile can estimate and submit an order using backend-authoritative pricing.
- Authentication return preserves draft information; no information leakage in URL.
- `new -> review` occurs exactly once on explicit open; passive GET changes nothing.
- Admin sends versioned proposal; approval without amount fails; the order does NOT start printing at quote send time.
- Customer-only quote accept; wrong user, stale revision, replay/duplicate, concurrent edit and superseded quote are rejected safely.
- Accept updates quote status, order status and `final_price` together; rollback on partial failure.
- Customer revision request reopens review; only new revision can be accepted; decline ends without production.
- Production start is blocked before customer acceptance; historical orders remain compatible.
- `printing -> binding` when needed and direct `printing -> ready` for only-print.
- Tracking stays available via account without tracking code/mobile; admin notes never leak.
- Order-related support thread ownership, status and message chronology; quote discussion is not an implicit acceptance.
- All key API contracts continue to work, including old order/ticket data.
- In-app notifications: correct recipient per event, persistent unread count, read/read-all, deep link, new support messages, quote revisions and progress transitions; no phantom/duplicate events and no cross-account exposure.
- Relevant frontend responsive, RTL, accessibility and error-state checks.

Run real scripts only after inspecting them. Run backend/API/architecture, browser, Typecheck, lint and safe non-publish builds; isolated SQL tests where available. No real SMS, no operational database writes.

## Phase 8 — Documentation and report

Update `AGENTS.md` with the new immutable business rule: **a proposal must be explicitly approved by the order owner before a new request can move to production**, and meaningful committed business events must generate durable in-app notifications for the correct audience. Update relevant skills only if general procedure changes, not to duplicate the entire task. Update existing architecture and order-flow documentation.

Create `docs/CHAP-REQUEST-TO-PRINT-IMPLEMENTATION.md` with:
- Current vs target flow and mapped state machine.
- Source files/controllers modified.
- API contract changes.
- Schema/migrations prepared and applied-to-isolated-DB results.
- Customer/admin/support/notification UX and screenshots if practical.
- Notification event matrix, admin/customer API contracts, deduplication/transaction guarantees, polling behavior and planned future push boundaries.
- Feature matrix and automated test results (passed, failed, blocked separately).
- Historic data and deployment compatibility.
- Remaining blockers and the exact next step to test manually.

## Strict boundary

Do not deploy, publish, package, modify IIS/VPS/DNS, run actual SMS or Push, apply operational migrations or delete/reset operational records. This request authorizes source changes and isolated verification only.

Do not claim production-ready or fully operational until approved migrations have been separately applied and end-to-end manual acceptance has passed.

Begin with actual repository inspection. Implement in coherent, reviewable phases and finish all feasible authorized work without pausing for routine confirmations.
