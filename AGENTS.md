# CHAP GITI — Codex Engineering Instructions

## 1. Purpose and operating principles

This repository implements **Chap Giti**, a Persian/RTL printing and publishing service. Customers can estimate book-printing costs, submit orders and PDF files, track production, and contact the print shop. Administrators review orders, set final prices, update production statuses, and manage content and pricing.

Follow a **pragmatic, production-ready** engineering approach: ship small, safe, understandable changes; preserve working behavior; test according to risk; and avoid speculative abstractions or enterprise-scale complexity.

These instructions apply to all work in this repository. If a more specific nested `AGENTS.md` exists, follow it for files within its scope as well. When instructions conflict with an explicit user request, surface the conflict and obtain a decision before making a destructive or high-risk change.

## 2. Inspect before editing

Before implementing a task:

1. Read the relevant existing code and contracts; do not implement solely from a task description or this document.

2. Check `docs/ARCHITECTURE.md`, `backend/README.md`, and relevant existing tests when applicable.

3. Identify affected business rules, frontend/backend interfaces, data storage, security, and deployment assumptions.

4. Reuse established patterns unless they are demonstrably unsuitable.

5. Keep the diff focused. Do not refactor unrelated code or silently change business behavior.

The project handover document describes the system as of **2026-10-24**, not necessarily the current code. Verify repository reality before relying on file paths, versions, commands, or features.

## 3. Technology and architectural boundaries

Maintain the current stack unless the task expressly authorizes a change:

- **Frontend:** Next.js App Router, React, TypeScript, Tailwind CSS, Persian RTL design.

- **Backend:** C# / ASP.NET Core targeting .NET 10, with Clean Architecture boundaries.

- **Database:** SQL Server; the existing operational database is **`ChapGiti`**.

- **Data access:** EF Core 10.x / SQL Server is the approved primary persistence target. Keep EF mappings and DbContext in Infrastructure; retain narrowly justified parameterized SQL for legacy migration, locking, atomic idempotency and SQL-specific projections.

- **Deployment:** Independently built frontend and backend, Windows Server / IIS reverse proxy and existing service scripts.

Respect backend dependency direction:

- `ChapGiti.Domain`: business rules, domain validation, pricing, order/ticket state transitions; no HTTP, SQL, or infrastructure dependency.

- `ChapGiti.Application`: use cases and abstractions; depends on Domain, not concrete infrastructure.

- `ChapGiti.Infrastructure`: EF repositories/Fluent mappings, shared transactions, justified SQL, private file storage, hashing, SMS and external integrations.

- `ChapGiti.Api`: HTTP endpoints, authentication/authorization boundaries, request/response mapping, composition root.

The frontend is responsible for UI, SSR, routing, client behavior, and its API proxy. **The backend is authoritative** for pricing, authorization, order creation, and persistence. Do not move business authority into the browser or the Next.js proxy.

Do not introduce microservices, CQRS frameworks, message brokers, event sourcing, a new state library, a new UI framework, or an additional database without an explicit task-specific justification and approval. Prefer simple modules and existing dependencies.

## 4. Business invariants and compatibility

Unless a request explicitly changes them:

- Customer login **must require mobile number and password**. OTP is used for new-account mobile verification and password recovery, **not** passwordless login. Inspect repository code before assuming a specific implementation status.

- Both the calculator and its rate-limited estimate API require an authenticated customer with a completed profile. Creating requests, decisions, private files and tickets requires the same customer identity.

- The calculator returns an estimate. The administrator sends a versioned proposal. The owner's explicit acceptance of the current proposal sets final_price. Starting production is a separate administrator action and requires the current approved PDF, the owner's acceptance of current delivery conditions and no pending correction.

- Always compute and validate authoritative quotes on the backend. Never trust prices or discounts sent by the frontend.

- Preserve the current order and ticket state-machine rules and reject unsupported transitions.

- Preserve order ownership and ticket ownership checks on the backend.

- Preserve request idempotency safeguards and order concurrency/version checks.

- Preserve existing admin-configurable tariffs and stored historical order specifications/price breakdowns.

- Online payment, inventory management, accounting, and production scheduling are out of scope unless explicitly requested. Authentication/OTP and administrator capabilities are subject to verified repository implementation; never infer implementation from requirements alone.

When changing pricing options, discounts, or formulas, inspect and coordinate both `backend/ChapGiti.Domain/Printing/PrintPricing.cs` and `frontend/lib/book-pricing.ts`, their consumers, API payloads, admin settings and tests. The backend remains the source of truth. Do not adjust commercial tariffs or pricing policy without an explicit requirement.

## 5. Database safety: hard requirements

The operational SQL Server database **`ChapGiti` contains existing project data**. Protect it.

- Never create or substitute a new operational database automatically.

- Never drop, truncate, reset, reseed, or bulk rewrite real data unless the user explicitly authorizes that exact operation.

- Do not run tests or fixtures that write to the existing operational database.

- Do not modify already-applied migration files, including `001_initial.sql`.

- For schema changes after the immutable SQL001-009 boundary, add an EF Core migration plus model snapshot. Validate legacy schema/checksums before registering the EF baseline; keep Schema_Migrations separate from __EFMigrationsHistory. Review backward compatibility, backups and rollout order; never run migrations at startup.

- Prefer read-only checks when interacting with existing environments. Use a clearly isolated, disposable test database for write-based integration tests, with explicit configuration and safeguards to prevent pointing at `ChapGiti`.

- Never silently execute production migrations or irreversible data changes while implementing application code.

- Keep SQL parameterized, transactional where appropriate, and safe against injection and partial writes.

- Preserve amount units and integer semantics: the current UI displays **toman** and order amounts are stored as `bigint`.

If isolated integration-test infrastructure is unavailable, report the limitation; **do not substitute writes to real data**.

### Controlled Development migrations

The owner's standing authorization of 2026-11-23 covers required, reviewed migrations only on the existing local Development `ChapGiti`. This section governs current schema deliveries and supersedes older source-only directions and one-time backup waivers. A narrower instruction for a particular task still applies.

1. Follow **Schema change → Migration → Preflight → Backup → Apply migration → Verify → Tests → Development startup**. Compare the source, compiled migration inventory, both histories, legacy checksums and actual schema. Confirm the local machine, default SQL instance, Shared Memory and database identity using private configuration. Stop on drift, an unexpected pending set or a remote target.
2. Stop only this checkout's Development processes. Recheck its listeners and active database writers immediately before maintenance. Take a fresh timestamped full `COPY_ONLY` backup outside Git with SQL backup checksums and verify it with `RESTORE VERIFYONLY WITH CHECKSUM`. Record the recovery path, expected changes and pre-change counts or digests before running maintenance.
3. From the source root run `npm.cmd run db:setup` with `NODE_ENV=development` and the verified local connection. The runner applies its entire pending sequence. If already current, verify the actual schema and report any runner invocation as a no-op. Inspect history and schema after a failed attempt before considering a retry.
4. Check keys, constraints, indexes, preserved data and zero pending migrations. Run targeted and full regression, then start Development with `npm.cmd run dev` and check live health. If a required migration remains unapplied, report **BLOCKED - MIGRATION NOT APPLIED** and the actual reason. Keep startup read-only and mutation tests isolated.
5. Production requires separate deployment authorization, verified Release compatibility, an environment-specific verified backup and manual migration verification before activating the application version. Historical Development permissions do not authorize Production.

[Development and production migration runbook](docs/DATABASE-MIGRATION-FA.md#development-migrations)

## 6. Security and privacy

- Do not commit credentials, API keys, real connection strings, private files, user data, `.env.local`, or machine-specific secrets.

- Preserve secure password hashing, random session tokens, hashed server-side token storage, logout/session invalidation, and permission checks.

- Enforce authorization and ownership in backend use cases/endpoints, not solely in the UI.

- Maintain CSRF/origin protections and rate limiting around sensitive endpoints.

- Validate uploads by size, type, ownership, and storage path; PDFs must remain in private storage. The current documented limits are **PDF only, max 15 MiB per file and 5 files per order**, subject to checking the current code.

- Avoid logging passwords, session values, sensitive documents, or personal information.

- Treat HTTPS as the preferred production security target. The documented HTTP deployment is a known limitation, **not a security best practice**. Do not silently alter existing IIS bindings, ports, cookies, or proxy behavior as a side effect of unrelated tasks.

- For any auth, upload, permissions, or public exposure change, describe the threat/abuse cases and add relevant negative tests.

## 7. Frontend and UX rules

- Keep the user-facing experience **Persian, RTL, responsive**, and consistent with the existing brand and typography.

- Reuse existing UI components, design tokens, forms, layouts, and API client conventions before introducing new ones.

- Prefer clear, accessible forms: meaningful labels, focus states, keyboard usability, actionable errors, and loading/empty/success states.

- Use client components only where interaction requires them; preserve appropriate SSR/SEO behavior for public pages.

- Keep validation and TypeScript types aligned with the actual API contracts; do not invent fields or assume success-only responses.

- Handle authentication, validation errors, rate limits, conflicts, network failures, and loading states relevant to the feature.

- Avoid adding dependencies for trivial UI tasks.

- Maintain privacy/noindex behavior for private routes; do not enable public indexing or change canonical/site URL settings without a deliberate release decision.

## 8. API and backend implementation

- Follow established endpoint naming, response shapes, error handling, and authentication mechanisms unless a change is explicitly requested.

- Avoid unnecessary breaking JSON contract changes. If a breaking change is unavoidable, update all consumers in the same task and document the impact.

- Validate untrusted inputs and return appropriate HTTP errors (including 400/401/403/404/409/429 when relevant).

- Use **ASP.NET Core API Controllers** as the target HTTP entry-point pattern. Migrate legacy dispatchers incrementally, preserving routes/contracts where practical; avoid parallel duplicate routes. Keep controllers thin and put behavior in Application/Domain layers.

- Respect cancellation, failures, transactions, idempotency and concurrency where relevant.

- Preserve the existing order workflow and final-price requirement.

- Use narrow interfaces and abstractions where they clarify dependencies; avoid one-off framework layers.

## 9. Testing policy: proportional and mandatory

Every behavior-changing task must have **appropriate verification**. Do not declare a task complete based on build success alone.

Choose tests by affected risk:

- **Domain/business logic:** unit tests for normal cases, boundaries, invalid combinations, and deterministic pricing/rounding.

- **Application use cases:** focused tests for success, validation failures, permissions, state transitions, idempotency, and concurrency when relevant.

- **Repositories/API/integrations:** integration tests when behavior crosses boundaries; use isolated infrastructure only for write tests.

- **Interactive frontend features:** tests for important user behavior, validation, and failure states when an existing test setup supports them; add a lightweight test setup only if justified.

- **Critical journeys:** maintain/add E2E coverage where appropriate for login, estimate, order submission, and admin review; use a safe non-production environment.

- **Bug fixes:** add a regression test that reproduces the original problem when practical.

- **Pure copy/style changes:** typecheck, lint, build and targeted visual/manual verification may be sufficient; do not create artificial tests.

Prefer existing test frameworks and scripts; do not install a new testing stack for a small task without justification. Never mutate live data to achieve test coverage.

At minimum, run the relevant subset of existing checks when available, such as:

```bash

npm.cmd run typecheck

npm.cmd run lint

npm.cmd run test:architecture

npm.cmd run test:db

```

These are documented commands, **not a guarantee that they remain present or work on every operating system**. Inspect the current scripts first. `test:db` is documented as read-only; confirm it is still safe before running. Run backend builds/tests and frontend builds/tests that are relevant to the change. Record what actually ran, passed, failed, or was not runnable, with the reason.

## 10. Delivery, deployment, and documentation

- Respect independent frontend/backend builds and their shared API contract.

- **Standing rule: no deployment, publish, packaging, or release artifacts until the project owner explicitly authorizes that operation in a later request.** This applies to all tasks, not only the authentication modernization.

- Do **not** run `dotnet publish`, `npm run publish`, `npm run publish:frontend`, `npm run publish:backend`, `npm run package:iis`, deployment scripts, or equivalent operations, and do not create release ZIPs or `frontend/publish` / `backend/publish` outputs.

- Safe local compile/build, typecheck, lint and tests are permitted only after confirming the invoked scripts do **not** trigger publishing, packaging, deployment or writes to operational data.

- Do not deploy, restart IIS/Windows services, change server bindings, enable SMS, toggle indexing, or modify real environment settings without explicit authorization.

- Preserve existing private storage and server-specific settings during packaging/deployment.

- Consider rollback and backup requirements for stateful changes.

- Update documentation when behavior, architecture, API contracts, migrations, setup, or deployment instructions change; keep documentation succinct and accurate.

- Avoid unrelated formatting, generated artifacts, temporary files, or legacy cleanup in feature diffs.

## 11. Task workflow and completion report

For each task:

1. **Understand:** summarize affected behavior, source files, and constraints.

2. **Plan proportionally:** small fixes can proceed directly; broad architecture or schema changes need a concise plan and risk assessment.

3. **Implement:** make the smallest coherent change, respecting architecture and compatibility.

4. **Verify:** run risk-appropriate tests/checks and inspect failure paths.

5. **Report:** clearly distinguish completed work from assumptions, blocked checks, and recommendations.

Final Codex report should include:

- Summary of changes and affected areas/files.

- Any intentional changes to business rules, endpoints, data schema, or user experience.

- Tests/checks actually executed and results; explicitly list skipped or blocked tests.

- Database/migration impact and whether any real data was touched.

- Security, deployment, compatibility concerns and remaining known risks.

**Never claim a test, deployment, production verification, or data migration succeeded unless it was actually performed and its result was observed.**

## 12. Sources of truth

Read relevant repository materials as needed:

- `docs/ARCHITECTURE.md` — architecture and boundaries.

- `docs/PROJECT-HANDOVER-FA.md` — project handover and documented baseline.

- `backend/README.md` — backend setup and details.

- `frontend/lib/book-pricing.ts` and `backend/ChapGiti.Domain/Printing/PrintPricing.cs` — pricing implementation.

- `backend/ChapGiti.Api/Controllers/` and `backend/ChapGiti.Api/Http/ApiRegistration.cs` — actual MVC routes, filters and composition.

- `backend/ChapGiti.Infrastructure/Persistence/Migrations/` — SQL migrations.

- `deploy/iis/README-DOTNET.md` — current IIS deployment procedure.

**Repository code and verified execution are the authority for actual current behavior; documentation is the starting baseline.** When documentation conflicts with current code, note the mismatch and resolve it within the task's scope rather than silently choosing one.

## 13. Approved identity, authorization, and admin UX target

These are **approved requirements for upcoming implementation**, not claims that the repository already supports them. Implement them only when the corresponding task is in scope; do not accidentally broaden unrelated changes.

### Identity and OTP

- Customer **login always uses mobile number plus password**. Do not enable OTP-only or passwordless login.

- New registration must proceed mobile → SMS OTP verification → profile/password completion → persistence; never mark the number verified before a successful OTP challenge.

- Forgot-password recovery must use purpose-bound SMS OTP followed by setting a new password. The owner-approved eligibility policy rejects unknown accounts with `CUSTOMER_ACCOUNT_NOT_FOUND` before challenge creation or SMS; pending registrations are not completed accounts. Incomplete profiles and unverified legacy mobiles cannot recover automatically. Revalidate eligibility during verification and revoke stale challenges without creating a reset grant. Preserve IP/phone request limits and legacy password login/phone verification.

- OTP codes are generated by the MelliPayamak Console provider in the enabled integration. Verification challenges must be short-lived, one-time, securely stored, purpose-bound, attempt-limited and rate-limited; add resend cooldown, replay protection and safe provider-failure handling. Reuse the existing MelliPayamak adapter when appropriate. Never log or return OTP secrets.

### Real OTP in local Development and the explicit test switch

- Real OTP verification is enabled in the current local Development environment: `OTP_VERIFICATION_ENABLED=true`, `OTP_ENABLED=true`, `SMS_ENABLED=true`, four-digit codes and a 120-second lifetime. Do not silently restore the old development bypass. These enable flags do not establish that credentials exist or that delivery succeeded.

- Read the console API key and notification credentials only from existing private local configuration. Never hardcode, print, return or log credentials or OTP values. The existing console OTP endpoint generates and sends the code; the application validates its format and stores a purpose-bound HMAC. Do not claim to have tested the external provider's random-number generator.

- Console OTP uses only `POST https://console.melipayamak.com/api/send/otp/{API_KEY}` with a `to`-only body. Treat the returned `code` as the OTP; application randomness is for challenge/session/grant identifiers, never a replacement SMS code. Parse `status` as diagnostic text, not a boolean or an invented success enum. Require HTTP success plus a string code of the configured numeric length and the documented JSON shape; a non-empty string status alone is not an error, and HTTP 200 or a status message without a usable code is not success. Cover both registration and recovery with the real adapter over a mocked HTTP transport.

- `OTP_VERIFICATION_ENABLED=false` remains an explicit opt-in for isolated local testing only. It permits any exactly four-digit code only for registration and password recovery and suppresses all real SMS sending. Keep the mobile → OTP verification → profile/password UI sequence.

- Disabled verification is permitted only in Development with a loopback `Site:Url`; reject this setting at startup in Production, Staging or a public-site configuration. Missing settings outside Development default to verification enabled; invalid booleans fail closed.

- `OTP_VERIFICATION_ENABLED=true` restores the normal configured MelliPayamak issue/send/HMAC-verification flow; provider credentials and `OTP_ENABLED` still have to be configured. Never silently fall back to bypass on provider failure.

- Bypass still requires a server-issued, purpose-bound, unexpired, unused challenge. Keep rate limits, resend cooldown, attempt limits, recovery eligibility, password validation and session revocation. Do not add passwordless login or bypass the authenticated legacy-phone verification operation.

- Bind challenges and recovery grants to the mode that issued them, so toggling the switch cannot make old development challenges/grants valid in normal mode. Do not rewrite existing database records or applied migrations to implement the switch.

- Cover both modes, exact digit length, disabled-SMS behavior, mode changes and environment guards in isolated tests. Unit tests and ordinary integration tests must use fake SMS transports and isolated/in-memory records, never real sends or operational customer writes.

- A real SMS smoke test requires explicit task-specific authorization; ordinary implementation and test requests authorize no real messages. If a later request authorizes one smoke test: no retries, no automatic verification/consumption, no password reset and no unnecessary account creation. If the console key/integration is unavailable, report the blocker without attempting another provider or fabricating success. Report provider acceptance separately from handset delivery.

- Do not deploy, publish, package or apply operational migrations while enabling or testing real OTP. Real local SMS authorization does not authorize production SMS activation or deployment.

### Separate customer/administrator authentication, profiles and session renewal

- Customer and administrator authentication MUST remain independent. Customers log in with mobile/password; administrators log in with the fixed username `admin` and their existing password at `/admin/login`. A customer login, even with a historical `Customer_Roles` assignment, never authorizes an admin endpoint. Every admin endpoint validates the administrator context on the backend.

- Use distinct `giti_customer_access` / `giti_customer_refresh` and `giti_admin_access` / `giti_admin_refresh` HttpOnly cookies. Token digests are domain-separated by the v2 customer/administrator scheme; verify the principal kind too. Scope status, rotation, reuse detection, logout and password invalidation to the correct context. Customer renewal uses `/api/customer/session` and `/api/customer/refresh`; admin renewal uses `/api/auth/session` and `/api/auth/refresh`.

- Old shared cookies are ignored; users sign in again after adopting this version. Keep existing credentials and records unchanged. Do not rewrite sessions or infer independent administrator identities from customer roles, names, phone numbers or emails.

- Administrator username `admin` is immutable and not editable in any profile, creation, setup or login flow. Resolve the existing `Admin_Usernames` mapping; never select the first administrator, infer an owner from email or create/remap a username during login. Reject email-based login payloads and any other username. Preserve the existing mapped account and password.

- Administrators may change only their own email/password through `/api/admin/profile/email` and `/api/admin/profile/password`. There is no target user ID. Require the current password for password changes; reject unknown request fields, duplicate/invalid emails and password mismatches. Revoke all administrator sessions on password change without affecting customers.

- Global NEW/CHANGED password policy: 6–128 UTF-16 characters, at least one English uppercase A–Z and one printable ASCII punctuation character. Use Domain `PasswordPolicy` and matching reusable frontend validation/guidance. Never trim passwords or enforce the new complexity at historical-password login. Apply the policy to registration, OTP recovery, admin-created customers, any explicitly authorized administrator provisioning and the explicit setup CLI.

- Show this guidance before submission and validate inline: «رمز عبور باید حداقل ۶ کاراکتر، شامل یک حرف بزرگ انگلیسی (A-Z) و یک کاراکتر خاص مانند ! یا @ باشد.»

- Existing customer/admin accounts, historical order/ticket ownership, and administrator access must survive an explicit, backward-compatible identity migration. Do not grant roles implicitly while reconciling identities.

- Customer registration sequence is mobile number → provider-issued OTP verification → profile/password completion → account persistence. A server-validated verification grant must bind the verified mobile number to subsequent account creation, and be consumed on success. For an existing number, guide to password login. Do not collect profile data before OTP verification. This approved account-presence lookup is rate-limited and must not return personal profile data, create a session or send SMS; recovery independently verifies completed-account and verified-mobile eligibility before issuing any challenge.
- Registration grant API: `otp/request` accepts phone/purpose only; `otp/verify` consumes the code and issues a five-minute, purpose/mode-bound grant; `registration/complete` validates that grant with profile/password and consumes it atomically with account creation. Store only the full grant digest as a separate logical grant row in the existing OTP table. Physical storage uses `purpose='register', delivery_status='pending', attempts=5, code_hash=NULL`; Infrastructure maps this reserved non-OTP combination to logical `grant`. Never write literal `grant` into delivery_status: deployed `CK_OtpDelivery` permits only pending/sent/failed. Exclude encoded rows from SMS counts/cooldown even after consumption/invalidation. Validate repository commands against existing CHECK definitions in regression tests. Do not introduce an operational migration, use old pending profile/password rows, or store grants in URLs/browser storage/cookies.

- Customer password login normalizes the mobile number before lookup. A missing account returns HTTP 401 with `CUSTOMER_ACCOUNT_NOT_FOUND`; a wrong password on an existing account returns HTTP 401 with `CUSTOMER_PASSWORD_INCORRECT`. This explicitly approved account-presence distinction retains both IP and normalized-phone rate limits and dummy password verification for missing accounts. Frontend behavior must use these codes, never match error text. Show the specified Persian guidance, link the missing-account error to registration with the entered mobile retained in client state, and always keep a registration link below the password form and password recovery accessible. Do not place mobile numbers or passwords in navigation URLs. Administrator errors and authentication remain unchanged. Recovery now uses the separately approved eligibility policy above. Cover both errors, numeral normalization, API codes, link prefilling and password login regressions with isolated backend/browser tests.

- The post-registration success action must explicitly open a fresh password form while retaining the verified phone and safe return destination. A same-page Next.js Link can preserve the old client state; test the actual action without a test helper reloading the page. Never put an administrator username option on the customer form. Old legacy query bookmarks redirect to the dedicated admin route; administrator login accepts only the fixed username `admin` and password; profile email is not a login credential.

- Support secure refresh credentials/session renewal, with server-side revocation, rotation, expiry, concurrent-request safety, refresh reuse handling, logout invalidation and password-reset invalidation. Prefer HttpOnly cookies and avoid localStorage for long-lived tokens.

- **Transport warning:** the documented public HTTP deployment cannot protect credentials in transit. Do not weaken cookie/token security to hide this limitation; call out HTTPS as a prerequisite for secure public production rollout, without changing deployment settings unless authorized.

- Admins may list/search and create customer accounts; they cannot edit another user’s personal information. Customer roles are retained as historical data only. The old customer role mutation endpoint rejects requests without writes; the `/admin/administrators` directory is read-only and its creation endpoint rejects with 409 under the fixed-username model. Preserve historical administrator accounts and do not reassign the admin mapping. Do not add account deletion or another administrator’s credential editing.

- Initial passwords must be individually chosen, compliant and shared privately. Never mark an admin-created customer’s mobile verified without that customer’s ownership proof.

- Migration 004 adds nullable `Admin_Users.login_email` and a unique computed `effective_email`, retaining the original email key and all references. Despite the historical column names, these fields now store profile/contact email only; username login resolves `Admin_Usernames` independently. No new migration is needed for the username correction. Local Development applied 004 with explicit owner authorization on 2026-10-30; the backup waiver was limited to that operation. Do not infer another environment's migration state or apply migrations there without its matching authorization/recovery requirements. Before 004, repositories fall back to the original email for login/list/session/password functionality; only email editing returns an unavailable error. Do not make startup depend on 004. Keep the SQL SET options required by the computed index.

- Admins **must not edit other users' personal data**, through UI or API. Role assignment/revocation is a separate, limited authorization operation and must not be implemented as a generic profile-update endpoint.

- Users can update only their **own allowed profile fields** on the website. Continue prohibiting direct mobile-number edits unless a separately approved ownership reverification flow is designed. Never allow a profile update to modify roles or account secrets.

### Admin content user experience

- Banner **add and edit** forms must open in accessible, RTL-friendly modals; do not regress upload, validation or list refresh.

- The magazine is active in public `/blog` and administrator article management. Preserve historical articles, images, slugs, recorded authors/dates and publication states. Portfolio stays retired and its legacy public/admin routes keep their compatible redirects.

### Calculator regression rule

- Investigate the existing `pages is invalid` calculator bug at the frontend/backend contract and parsing boundary; fix the actual cause without disabling authoritative backend validation or changing commercial price rules. Test integer bounds and Persian/Arabic numeral normalization as applicable.

- Customer order summaries show aggregate estimated/final prices. The customer calculator result lists exactly eight rows in this order: selected cut, copies, finishing per copy, unit price before discount, discountable subtotal, quantity discount, unit price after discount and final amount. The spine estimate and print-request action follow the table. Preserve quote API calculations, tariffs and historical `priceBreakdown` data; administrator order details may render the stored breakdown without recalculating it.

- Keep authenticated `/account/orders` discoverable and paginated. Remove `/track` from the public main navigation; retain its route for authenticated owners following historical links. Customer order details enforce ownership and reuse tracking/history behavior, adding only the owner's saved specification and safe file metadata (never storage keys, admin notes or pricing breakdown). The existing `GET /api/customer/orders` array contract remains available without `page`; paginated requests use `page`/`pageSize`. Do not add schema changes or operational test records for these views.

### Implementation and tests

- Use phased, reviewable modifications for cross-cutting identity and API changes. Preserve existing URLs and JSON contracts whenever practicable; confirm migrations and rollback considerations before identity/schema changes.

- Add risk-appropriate negative/security tests covering OTP expiry/replay/limits, password recovery, refresh rotation/revocation/concurrency, role authorization, self-profile ownership and blocked admin profile writes.

- Prepare additive database migration scripts as needed, but **never apply them to operational `ChapGiti` without explicit permission**. Perform write-oriented integration tests only against a verified isolated disposable database.

- No deployment, publishing, packaging, production SMS activation or release generation is authorized by these requirements.

## 14. Owner decisions that supersede older requirements

### Main local Development database (2026-11-18)

- Normal `npm.cmd run dev` in PowerShell and the VS Code terminal uses the existing `ChapGiti` catalog on the default local SQL Server via Shared Memory. Keep credentials in private configuration. The old test selector is not a daily startup step.
- The owner authorized reviewed SQL008/009 and the two EF adoption migrations on this local catalog after a verified full backup. Historical SQL001–009 remains immutable. Track new EF migrations, designer and snapshot with the corresponding Infrastructure model, review generated SQL and apply through explicit maintenance after target verification and backup. Startup stays read-only. This decision grants no release, remote operation or future schema change.
- Preserve existing accounts, settings, orders and files. Leave obsolete sandbox databases and private credentials alone. Never run mutation fixtures against main Development. Creating a disposable SQL database for later acceptance requires separate authorization. See `docs/CHAP-MAIN-DEVELOPMENT-ACTIVATION-FA.md`.

### Current source-only Onion/EF/catalog/magazine modernization

- Follow-up migration acceptance/runbook work uses the same source-only and no-operational-access restrictions. The guarded `--authorize-disposable-ef-activation` test additionally checks the actual maintenance command, rich legacy history preservation and real SQL-backed Next pages; see `docs/CHAP-EF-MIGRATION-ACTIVATION-FA.md` and `docs/CHAP-EF-MIGRATION-ACCEPTANCE-FA.md`. This does not authorize activation of working/server ChapGiti or a new release.
- The latest owner request approves EF Core 10.x as primary persistence and restores the database-driven magazine. This supersedes older ADO.NET-first/no-ORM and magazine-retirement instructions. Portfolio and the article-footer calculator/order CTA remain disabled. General and order-linked support tickets are active. Customer/admin identity, OTP, quote acceptance, correction review, production gates, private PDFs and independent notifications/badges remain unchanged.
- Implement a validated EF baseline at the immutable SQL001–009 boundary; preserve `Schema_Migrations` and use separate EF history. Do not rewrite the legacy SQL chain, use EnsureCreated, mark an unchecked baseline applied, or run startup migrations. New EF-owned schema must have tracked EF migrations/model snapshot and documented fresh/upgrade manual commands. Verify SQL2019 and SQL2025 only where those engines were actually exercised.
- Catalog IDs/codes are stable. Public options come from SQL; names/dimensions/active/default/order and supported pricing metadata are admin-configured, versioned and audited. Do not edit historical order/quote snapshots or invent prices/technologies. Inactive/unpriceable selections fail explicitly; required defaults must be active.
- Magazine reuses Articles and existing image storage/routes, supports safe defined content, draft/published/archived transitions, concurrency, recorded publication timestamps and URL compatibility. No invented authors/dates, automatic draft publication or parallel CMS. Public APIs/SEO/sitemap expose only published records; unpublish must invalidate availability immediately. Keep environment indexing policy unchanged.
- Current authorization is source/dependencies/documentation, safe non-publish builds and tests, including writes solely in positively pinned disposable SQL environments. The completed release authorization below is historical, not permission for this task. No Publish, ZIP, deploy, IIS/Windows Service changes, working/server database access or migrations, real SMS/Push, destructive Git operations or commit/push. Preserve private settings and all existing published/release artifacts.

### Completed local Windows Server release authorization (historical, 2026-11-17)

- For that completed release task only, the owner authorized local Release builds, standalone frontend/self-contained win-x64 backend publishing and exactly one release-only Windows Server ZIP from the dirty working tree. That historical authorization is not permission to publish/package the subsequent Onion/EF/catalog/magazine task. Preserve existing archives; no Source ZIP, commit/push, server deployment, IIS/Windows Service execution, working/production database access or real SMS/Push was authorized.
- New-install defaults are `Giti`, `GitiNode`, `C:\Giti\app`, `C:\Giti\data`, `C:\Deploy\Giti-Release` and `C:\inetpub\giti-proxy`; keep explicit path/service parameters for legacy installations. WinSW executable/XML identity, launch arguments and working directory must agree. Prepare scripts without running installers. Keep the existing trusted client-header contract despite the renamed installation.
- Release-only packaging requires current successful verification and a guarded staged published-output smoke test. The opt-in `--authorize-disposable-published-release <workspace-release-root> <.qa-result>` test accepts no operational SQL connection: it creates/pins its own LocalDB instance/catalog, runs the bundled real migration CLI/wrapper, starts published processes with synthetic configuration and SMS/OTP delivery disabled, and cleans up only its own fixtures. Ship no development test fixtures.
- Legacy `test-readonly.mjs` and its aliases now fail before configuration/SQL access unless a separately authorized `--authorize-configured-database-readonly` flag is supplied. They are not unit tests and must never be included in ordinary frontend/release batches. During this release, an obsolete alias was initially executed and performed configured-database reads; no write/migration was executed by that path. Record this incident accurately instead of claiming zero operational access.
- Include every actual SQL migration and normalized/raw checksums; never change existing scripts or apply them to working/server databases. SQL Server 2019 LocalDB acceptance does not establish SQL Server 2025 Express acceptance; that destination requirement must remain explicit in release documentation. Report current audits, tests, native/standalone smoke results, ZIP CRC and manifest hashes, file count, size and external SHA-256.

### Isolated SQL acceptance authorization (2026-11-17)

- The owner explicitly authorizes migrations and synthetic fixtures only in a positively verified disposable SQL environment for 008/009 acceptance. The 2026-11-16 source-only/no-SQL restriction below described that earlier task; it does not prohibit this later specifically authorized isolated test. Operational/working `ChapGiti` and deployment databases remain entirely off limits, including reads.
- Reuse the opt-in `tests/sql005` infrastructure with `--authorize-disposable-localdb-008-009`. Its newly created random LocalDB instance must be empty of user databases before creating the disposable catalog. Accept no environment/operational connection string. Pin actual LocalDB server identity and catalog before migrations, fixtures, API operations and cleanup; a test-looking catalog name alone is insufficient. Use the real migration runner, checksum normalization, locking and transactions. Keep ordinary `scripts/sql-test-fixture.mjs` disabled.
- Cover upgrade from 001–007 and a fresh complete chain, synthetic historical records/private PDFs, current correction/quote gates, real notification/order-view persistence, concurrency, rollback and authorized ticket mutations. Report the actual SQL engine build; do not infer acceptance for a different deployment version. Do not change applied migration files or rewrite recorded checksums. Never activate features in the working database as part of acceptance.
- Keep the approved correction, PDF, independent order-attention and active support policies below. No publish/package/deploy, IIS/Windows Service changes, real SMS/Push or automatic Git commit/push is authorized. Creating/stopping the fixture's own LocalDB process is not permission to touch existing instances or services. Document results and blockers in `docs/ORDER-WORKFLOW-SQL-ACCEPTANCE-FA.md`.
- Isolated acceptance completed on SQL Server 2019 LocalDB 15.0.4382.1; the fresh 001–009 chain and historical 001–007 upgrade through 008/009 passed real SQL/API/WebSocket checks. See that report for evidence and limitations. This result does not establish the working database's migration state, authorize later operational access, or prove compatibility with another engine version. The fixture removes only its own databases, instances and synthetic private files.

### Order corrections, attention and customer support (updated 2026-11-18)

These approved requirements supersede older support/consultation and request-information behavior below and in the project skills.

- Extend the existing Clean Architecture and controller/application/repository boundaries. `OrderCorrectionService` coordinates explicit request → customer submission → administrator approval/rejection. Keep `review` for outstanding corrections and preserve immutable approved/rejected cycles and all PDF versions. Superseding a pending cycle retains its request/submission evidence. Terminal orders cannot be resurrected by reviewing a correction.
- The administrator explicitly chooses `requiresFile` for each correction. Required corrections accept only a new valid private PDF linked to that same order/correction; a prior attachment and a forged client flag cannot satisfy it. Text-only corrections impose no additional PDF requirement; the original approved primary PDF is still required for production. Rejection requires a public reason.
- `OrderProductionReadiness` is the centralized production gate, invoked inside the order transaction/lock by explicit start and legacy PATCH-to-print. Pending/rejected/superseded corrections, unavailable/invalid required documents and unaccepted latest prices block printing. Only the latest approved correction/file controls production. Preserve order versions, command receipts, transactional events/notifications and accepted quote history. After owner acceptance no ordinary re-quote is permitted. New or revised nonfinancial final terms bind the accepted quote to the approved primary PDF and correction; explicit acceptance of the current terms is required before production.
- Every new print request requires a valid privately stored primary PDF and explicit terms/privacy consent. Legacy service flags do not govern new orders. Preserve original files, correction history, per-file size limits and ownership.
- Order attention is separate from notification `read_at`: migration 009 provides per-order, typed-recipient `Order_Views`. Count distinct owned orders with newer persisted order-notification sequences than their last observed detail sequence/activation baseline. Lists, menus, SSR/prefetch and notification read/read-all never mark orders seen. Only a visible authorized detail sends an explicit view acknowledgement; stale acknowledgements cannot consume newer activity. Customer/admin contexts and sessions remain independent. Reuse the existing authorized notification summary and SignalR/polling provider; no parallel counter transport.
- Both notification interfaces use «اعلان‌ها», automatic updates and error recovery, without a routine refresh button. Order attention and the notification bell have role-scoped badges, hidden at zero and capped at `99+`.
- General and order-linked ticket intake, replies, close/reopen, persisted unread state and customer/admin SignalR support are active. Authorize ticket ownership before any access. Only dispatch after transaction commit. Support messages never authorize financial, file or delivery decisions.
- The contact heading is «ارتباط با چاپ گیتی». Use only existing configured phone, email, address, hours and supported Instagram/Telegram/Bale links. Respect RTL, wrapping, responsive cards, keyboard focus, `tel:`, `mailto:` and safe external attributes; never invent contact details.
- Prepare only additive `008_order_corrections.sql` then `009_order_views.sql`; never edit applied 001–007. 008 adds correction/file references and removes the obsolete one-accepted-quote-per-order uniqueness restriction while retaining historical acceptances. 009 records an activation sequence baseline rather than fabricating historical views. Pending migrations return explicit unavailable responses for the new commands; no automatic migration/startup writes.
- Historical source-only tasks did not grant operational database or release access. Follow the current task and section 5 for guarded local Development migrations and specifically authorized content writes. Without disposable-SQL authorization, use memory-only API/browser fixtures and source-contract checks and report SQL-engine acceptance as unverified. The current Onion/EF task explicitly authorizes the guarded disposable acceptance paths described above; the generic configured-database fixture stays disabled. Compile safely with `CHAP_GITI_VERIFY_BUILD=true` and `npm.cmd run build:frontend`; root `npm run build` publishes and remains forbidden.
- Verification commands: `npm.cmd run test:auth`, `node tests/order-workflow-schema.mjs`, `node tests/modernization-ui.mjs --order-revisions`, existing `--request-workflow`, `--notifications`, `--realtime`, CMS and authentication branches, Typecheck, Lint and architecture checks. Tests seed historical tickets directly in their isolated memory fixture and never call live support mutations expecting success.

- The administrator login is **independent** of customer login and accepts fixed username `admin` plus password; the administrator may change only their own contact email and password. Never infer admin access from a customer session or a customer role.
- Customer registration order is **phone → real MelliPayamak Console OTP → profile/password → account creation**. The older phone → profile → OTP sequence is obsolete. Never collect or persist the completed profile before the OTP verification step.
- Login UI must give an actionable account-not-found message and registration link for an unknown customer number, and an incorrect-password message for an existing account, using structured backend error codes.
- Show the selected cut, quantity and finishing in the calculator result alongside the monetary summary. Display the signed-in customer’s full first and last name in the public header at all screen widths and in the account greeting. Keep raw tariffs and private commercial snapshots in the backend. Show the accepted proposal separately. Customer order history is the primary tracking journey. Existing tracking URLs require customer sign-in and order ownership.
- The owner discussed clearing development customer data; **this discussion is not standing authorization to delete records**. Require explicit approval of a concrete deletion scope and verified backup before executing destructive cleanup.
- The OTP feature currently uses the real provider endpoint when enabled. Bypass and real sends require explicit matching task/configuration; automated tests never send real SMS.
- Recovery eligibility supersedes the former neutral-send policy: unknown accounts return HTTP 404 and `CUSTOMER_ACCOUNT_NOT_FOUND` with a registration link, no SMS/challenge and no OTP countdown. Unverified/incomplete accounts return HTTP 403 with `CUSTOMER_PHONE_UNVERIFIED` / `CUSTOMER_RECOVERY_UNAVAILABLE`. Distinguish incorrect, expired, missing, consumed, attempt-limited, invalid-state and provider-failed OTP errors with structured codes. Clear terminal recovery state, retain the mobile in component memory, and never advance to password reset based only on a challenge. Registration/admin authentication stay unchanged.
- **No deployment or publishing without a future explicit owner request.**

### Business and presentation update (2026-10-30)

- Use `frontend/lib/money.ts` (`formatToman`) for monetary displays: grouped amounts, تومان and bidi-isolated numeric text. Keep `money` for nonmonetary counts. Never convert persisted toman amounts or recalculate historical order snapshots merely to format them.
- All 15 finishing tariffs are global per-copy rates with the existing per-copy ceiling and quantity discount. The 11 printing rates are versioned by active catalog cut. Active fixed cuts start with 11 independent proposed amounts derived from the historical Chap Giti basis and catalog measurements. New estimates and order submissions require an administrator-approved revision. Approval appends a revision, even for unchanged amounts. Custom cuts require explicit base rates and validated dimensions. Keep historical global printing rates in storage only. [Tariff policy](docs/CUT-TARIFF-POLICY-FA.md). Save the effective version and rates with each new request. Business rate changes require explicit admin saves. Schema activation follows section 5 and preserves existing prices.
- Finishing eligibility mirrors Domain rules: a cover and binding are required; matte-marked options require matte coating; hard-marked options require hardcover binding. Plastic covers cannot use stale matte state to activate matte-only finishing. Explain disabled options and report selections removed after dependency changes. Do not infer unsupported commercial combinations from a reference website.
- V2 services use the Settings.services catalog, stable IDs, editable content, publication state and ordered public pages. Retain previous slugs as aliases with canonical redirects; the only service-specific primary action is consultation through authenticated support tickets. Marketing service slugs do not replace authoritative pricing/order identifiers.
- Remove only the MelliPayamak settings tab/panel. Keep private configuration, real Console OTP, recovery, notification delivery and SMS logs intact.
- Admin order queries include all statuses by default, support `new`, search and stable descending date/ID pagination. Lists refresh on focus and explicit refresh; aborted old responses must not replace current data.
- The single admin customer directory at `/admin/customers` reuses `/api/admin/users` with server pagination/search, order count and stored login/session evidence. Hide historical-role and phone-verification filters in the UI; retain compatible API parameters. `Auth_Sessions.created_at` records a fresh password login; refresh rotation is not a new login. Show last stored login/count and non-revoked unexpired session count without claiming online presence or inventing missing historical times. No admin editing of personal customer fields; `/admin/users` redirects to the directory.
- Business tests use isolated memory repositories and fake SMS. Operational SQL checks are SELECT-only; write integration tests require an explicitly isolated database. Root `npm run build` publishes and is prohibited for verification: set `CHAP_GITI_VERIFY_BUILD=true` and use `npm.cmd run build:frontend` instead.

### Order workflow and support update (2026-10-30)

- Domain `OrderWorkflow` is the transition authority. Explicit `POST /api/admin/orders/{id}/open` atomically changes new → review once with actor/time/event; GET, prefetch and lists are read-only. For workflow_version=2, review proceeds to a versioned proposal, explicit owner acceptance sets confirmed/final_price, then a separate administrator command starts printing. Direct review → printing is restricted to workflow_version=1 historical compatibility. Keep historical confirmed orders actionable without fabricating customer acceptance.
- Reject from review using existing terminal `cancelled` plus a required public reason. Keep the label compatible with historical cancellations. Persisted `spec.details.printOnly=true` skips binding: printing → ready; other/legacy orders use printing → binding → ready. Ready → completed; terminal states cannot restart production. Preserve version checks, transactions and event history. No automatic customer notifications for merely opening an order; existing status-change notification integration is retained.
- Admin price suggestion is not a persisted approval. Use a grouped text input accepting Persian/Arabic/English digits, send a numeric toman amount, validate server-side, and keep production-approved prices fixed. Show new proposal amounts as «مبلغ پیشنهادی (تومان)» to admins; legacy approval input labels remain compatible and «قیمت برآوردی» / «قیمت نهایی سفارش» to customers.
- Append private «یادداشت اجرایی سفارش» with idempotency key, actual author/time and no overwriting old `Orders.admin_notes`. Never expose either source to public/customer views. Old notes have unknown author/time; do not fabricate history.
- Migration `005_order_support.sql` adds nullable `Tickets.order_id` with FK/index and `Order_Notes` with FK/index. It was explicitly applied with 004 to the verified local Development database on 2026-10-30; this does not establish migration state anywhere else. Do not apply migrations during ordinary startup/testing. Before 005, general tickets and legacy notes still work; linked-ticket creation and note append return explicit 503 unavailable codes. Startup must not depend on 005. Preserve applied migrations; 004/005 are now immutable checksum artifacts in this local database.
- Support is one customer journey at `/tickets` («پشتیبانی من»), with general requests and owned-order entry from details. Validate order ownership in the backend. Retain open / awaiting_customer / closed and existing conversation history; use pagination, search, actual last-message preview, timestamps and distinct authors. No fabricated unread counts or real-time infrastructure.
- Contact is informational with configured business details and no duplicate submission form. Consultation uses `/contact`; portfolio stays retired. Magazine is restored under the latest requirements; calculator/order features remain independent.
- Navigation has one customer directory, support inbox and customer activity history. Remove duplicate users/admin-accounts/messages/SMS menu items, preserving admin profile, authentication, legacy message/SMS read routes and separate management audit. Never delete records merely because navigation changes. Do not display raw customer GUIDs.
- `/api/admin/activity` projects actual customer session creation, order/ticket creation and order events; no SMS-log relabeling, page tracking or invented logout. Revocation is not reliable logout evidence. Filter dates at Tehran day boundaries; retain Persian date/time display and stable server pagination. Never return session secrets, private notes or fabricated historical authors.
- Verification uses fake SMS and isolated memory API fixtures, including transactional rollback, concurrent open/note idempotency, ownership, privacy, pre-005 compatibility and context actions. SQL integration writes are skipped without an authorized disposable database. Use safe frontend verification build only.

### Explicit SQL acceptance exception

- With task-specific authorization for a disposable database, `tests/sql005` may create its own uniquely named SQL Server LocalDB instance and synthetic database. Current EF acceptance uses `--authorize-disposable-ef-activation`. Historical scopes include `--authorize-disposable-ef-modernization` and `--authorize-disposable-localdb-008-009`; `--authorize-disposable-localdb-005` describes the historical 005 task and is not the current workflow acceptance command. The fixture accepts no operational connection string, checks the pinned server/catalog before writes and API operations, and removes only its own fixture afterward. It is not part of ordinary test/startup/deploy scripts; retired broad SQL fixtures remain disabled.
- Real SQL acceptance found SQL 2717 in the previously unapplied 005: `nvarchar(5000)` is invalid. Keep note storage as `nvarchar(max)` plus `CK_OrderNotes_NoteLength` (`DATALENGTH(note)<=10000`) to enforce the existing 5000 UTF-16-unit limit, including trailing spaces. 001–004 were not edited. See `docs/SQL005-ACCEPTANCE-2026-10-30.md`; passing disposable tests never authorizes an operational migration.

### Verified local Development migration state

- The owner clarified that the configured, loopback-connected `ChapGiti` database is local Development with disposable test data and explicitly authorized 004 then 005 on 2026-10-30. For this operation only, the owner waived backup/restore testing. No further confirmation or backup was required by that authorization. Remote/production databases remain outside its scope.
- The existing runner committed 004/005 with reviewed checksums after verifying the local endpoint and the preceding preflight server fingerprint. The original rows/columns of all 25 existing data tables were preserved; existing 001–003 history was preserved too. This is not standing authorization for resetting data, changing credentials, running future migrations or waiving production backups.
- Local verification of profile email, linked tickets and private notes used a rollback-only SQL transaction; temporary sessions and all verification records were removed by rollback, without deleting existing accounts/orders. Ordinary write integration tests still use the isolated LocalDB fixture. See `docs/LOCAL-DEVELOPMENT-MIGRATIONS-004-005-2026-10-30.md` for results and current state.

## 15. Repository-local skills

For tasks matching their scope, read and use the relevant `SKILL.md` from `.agents/skills/`:

- `$chap-backend` — ASP.NET Core Controllers, Clean Architecture, use cases, auth and integrations.
- `$chap-frontend` — Next.js, TypeScript, responsive Persian/RTL forms, navigation and UX.
- `$chap-testing` — proportional unit, integration, regression and browser verification.
- `$chap-database` — SQL Server, EF Core/Fluent mappings, justified SQL, additive migrations and safe data operations.

Load only relevant skills, not all by default. This root `AGENTS.md` defines mandatory repository-wide rules; task skills provide specialized workflow guidance. Repository source code and verified behavior remain the authority on implementation status.

Open this repository as the Codex working directory for automatic discovery: scanning proceeds upward, so a session opened in the parent workspace does not discover this child repository's skills. Direct file reading still works. If the automatic skill list does not refresh after opening the repository, restart Codex there. Git rules allow only the four reviewed `SKILL.md` files; other private/generated agent files stay ignored.

## 16. Skill invocation and acceptance checks

- For any auth or registration task, use `$chap-backend`, `$chap-frontend`, and `$chap-testing` as applicable. Use `$chap-database` only when persistence or schema is affected.
- Registration regression tests must assert **mobile → OTP → profile → persistence**, existing-number redirect to login, attempts to skip OTP, expired OTP, and post-registration password login.
- Reporting a passing test is permitted only when its command was actually executed. Do not invoke release scripts while verifying.

## 17. Approved CMS/product V2 rules

- Services are fully administered through Settings.services JSON, with the exact six existing services as fallback only while the key is absent. Persist changes atomically under a locked settings read, preserve IDs, validate unique slugs/aliases and per-record versions, and never reset unrelated settings or tariffs. No new CMS table or migration is required.
- Public service lists/details/metadata/sitemap use published saved services. Renamed slugs retain canonical redirects. Consultation is the only primary service action and creates a real authenticated support conversation; preserve the customer login/registration return path.
- Banner CTA label/link are a paired optional value. No CTA means no button and no whole-banner link. Keep shared image uploads and historical banner records, including hidden retired portfolio promotions.
- Contact settings support optional Instagram/Telegram/Bale HTTPS account/channel URLs with exact platform hosts, no credentials or unsafe schemes. Empty URLs stay hidden; older clients omitting social keys preserve stored links. Never invent contact details.
- Magazine/blog is active and published-only in public/admin navigation, `/blog`, metadata and sitemap under the latest publication rules. Preserve article history and compatible slugs. Portfolio stays invisible in public/admin navigation and sitemap; its public/admin legacy routes still redirect to services. Keep Settings.portfolio/shared assets and suppress retired portfolio promotions without rewriting records.
- Home finishing cards are informational: no links, buttons, calculator preselection or action-looking hover behavior. Keep all actual finishing/pricing options in the calculator. Do not show an admin-login link in the public footer; /admin/login and independent administrator authentication remain operational.
- Reuse accessible RTL pagination with previous/next labels, current/total pages, first/last disabled states, safe bounds and consistent filter resets. Preserve backend pagination, ownership and deterministic ordering.
- CMS mutation tests use only explicitly authorized, positively verified disposable SQL fixtures; ordinary tests remain isolated/mocked. No operational writes/migrations, real SMS, deployment, publishing or release packaging are authorized by modernization tasks.


## 18. Approved request-to-quote-to-production workflow

- PRODUCT-ARCHITECTURE-FA.md is the approved business specification; MANUAL-UAT-FA.md is the manual acceptance checklist. Preserve unrelated CMS/auth/pricing rules.
- Estimation requires a completed customer session on the calculator page and rate-limited POST /api/quote. Keep its internal return path across sign-in or profile completion. Administrator sessions cannot authorize customer pricing. POST /api/orders and all decisions require authenticated completed customer identity. Preserve only schema-validated technical specifications in a tab-scoped authentication-return draft; never passwords, OTPs, grants, contacts, private notes or files, and never those values in URLs.
- New requests: new -> review -> awaiting_customer_approval -> confirmed -> printing -> optional binding -> ready -> completed. Customer revision requests return to review; decline and reasoned administrator rejection are terminal cancelled. Admin cannot accept for the customer. Passive GET/prefetch remains read-only; existing explicit POST open is exactly-once.
- Sent commercial terms are immutable revisions in Order_Quotes. Only the latest sent revision can be accepted. Acceptance, owner/time, final_price and confirmed status commit atomically. Preserve optimistic order/quote versions, actor/payload-bound Order_Commands receipts and rollback. Same-key exact retries are idempotent; changed payload/actor or stale revisions conflict.
- Review information requests stay in review and expose an explicit customer action; existing private PDF limits/ownership remain. Support discussion never implicitly accepts or revises a proposal. In-app events/status are authoritative; SMS is optional and must not gate transactions.
- Migration 006_order_quotes.sql widens status columns, marks existing orders workflow_version=1, defaults future inserts to 2, and adds proposal history/command receipts/information fields. The owner explicitly authorized 006 then 007 on the pinned local Development ChapGiti database on 2026-11-14; both are now applied there with reviewed checksums. This does not establish any remote environment's state. No startup migration or silent legacy fallback for new creation: missing 006 elsewhere returns ORDER_QUOTES_UNAVAILABLE/503 while historical reads remain available.
- Historical confirmed/production/final prices remain intact. Legacy review approvals are explicitly limited to workflow_version=1; issuing a proposal adopts version 2. Unowned historical requests cannot issue owner-approval quotes. Never fabricate historical customer acceptance.
- No deploy/publish/package, operational migration/write or real SMS is authorized by source implementation and isolated tests. Do not call the feature fully operational until separately authorized migration and actual manual UAT pass.

## 19. Durable in-app notifications

- Successful meaningful request/quote/production/support events produce persistent in-app notifications for the opposite audience. Use Domain BusinessNotification events and the Application NotificationPublisher inside the existing business UoW. A notification failure with schema 007 present rolls back the business change; no fire-and-forget, page-view events, actor echoes, false success or phantom notifications. Exact command/message retries and repository recipient+event deduplication must remain safe.
- Customer and administrator notification APIs and providers remain independent. Resolve recipients from SessionService, never request-supplied IDs. Administrator notifications and proposal foreign keys use the immutable Admin_Users.email key (SessionService admin.id), not editable profile email. Notify only the fixed admin username mapping. Existing unrelated legacy administrator records are not recipients.
- Store only safe template text, internal order/ticket reference and target, UTC creation/read time and dedupe key. Never copy messages, commercial amounts, private notes, PDF URLs, credentials or OTPs into notifications. Read and read-all are idempotent recipient-scoped projections; they never drive order transitions. A target is a whitelisted role-specific order or ticket route, and UI navigation waits for server read acknowledgement.
- The authenticated customer header and separate admin header share no cache/session. Each layout has its own keyed provider, unread bell, short dropdown and server-paginated center. The explicitly approved SignalR delivery in section 20 supplements the persistent HTTP/SQL path. Visible-tab fallback polls every ~20 seconds and connected safety reconciliation every ~120 seconds; refresh on focus/actions and synchronize read state. Opening the list does not mark it read. Recoverable GET errors never disable business actions; 401/403 clear stale private state. Do not add toast floods or extend realtime beyond the three approved areas.
- Migration 007_in_app_notifications.sql is additive with typed customer/admin FKs, unique dedupe indexes, read/time indexes and deterministic identity ordering. It was explicitly applied after 006 to the verified local Development ChapGiti on 2026-11-14; both checksum artifacts are now immutable there. Other environments still require their own inspection and authorization. Never auto-apply or backfill historical notifications. Before 007, notification APIs return NOTIFICATIONS_UNAVAILABLE/503 and pre-existing business behavior remains compatible without notification writes.
- The active channel is in-app only. Domain events and entity references permit future explicitly scoped mobile delivery; no device tokens/registration, push permission, service worker, Web Push, FCM/APNs or real phone push belongs in this phase. SMS/OTP behavior is independent.
- Regression coverage includes recipient/session isolation, immutable admin identity, versioned quote decisions, general/order-linked/consultation support, production, retry/dedup, atomic rollback, persisted read states, polling/hidden tabs, keyboard/RTL/mobile and guarded real SQL. Use tests/notifications.mjs and the --notifications browser branch; SQL writes remain confined to the opt-in disposable fixture. UAT-01 through UAT-30 remain manual NOT RUN until actually accepted.

### Authorized local activation and UAT preparation — 2026-11-14

- Database authorizations are target-specific. Verify the machine, server, database and migration history before maintenance. Previous approvals do not extend to another environment or another migration.
- The existing runner registered 006 then 007 with application locking, per-migration transactions and checksums. Before any login verification, all original rows/columns in 26 data tables were preserved by ordered SHA-256 comparisons; the two historical orders remain legacy=1 and original 001-005 history is unchanged. New structures are empty without fabricated acceptance or historical notification backfill.
- Local UAT uses process-only SMS_ENABLED=false and OTP_ENABLED=false, with OTP_VERIFICATION_ENABLED=true. Provider credentials are not forwarded to the running app. Do not weaken verification or silently rewrite .env.local; password login remains available and OTP-dependent manual flows require a separately isolated mocked transport. No Push implementation or sending is authorized.
- Explicit existing-account login verification may create ordinary authentication sessions, login audit and rate-limit rows; it never authorizes changing credentials, profiles or creating business records. Automated workflow/read-state writes still use disposable SQL, not the local customer database.
- SQL 1205 was reproduced while notification reads and read-state updates overlapped in disposable SQL. NotificationRepository retries only standalone idempotent queries/read-state statements, at most three total attempts. It never retries a statement inside a business UoW or replays notification insertion/business transitions; other failures remain visible and transactional rollback is preserved.

## 20. Approved scoped SignalR delivery — 2026-11-14

- The owner explicitly approved ASP.NET Core SignalR only for customer notifications, administrator notifications and support conversations. This supersedes earlier phase-specific prohibitions on realtime infrastructure only for these three areas. Login, OTP, pricing, CMS, uploads and order commands remain REST-driven; no phone Push or SMS transport is added.
- Two thin authorized hub types map to `/api/realtime/{customer|admin}/{notifications|support}`. Adapt the existing session cookies through SessionService; never issue hub-specific login credentials or accept recipient IDs/tokens in URLs. Keep role scopes independent even when both cookie families exist in one browser. Validate the configured Origin, secure-transport policy and current complete identity before establishing a connection.
- Authorize and canonicalize the ticket GUID before subscription. Groups are routing aids, never permission proof: recheck the existing session, revocation/expiry and current ticket ownership before every delivery. Use short-lived DI/SQL scopes rather than pinning a SQL connection for a WebSocket/SSE lifetime. Log scope/kind and safe error types, never cookies, provider keys, OTPs, message bodies or personal information.
- SQL persistence remains authoritative. CommittedUpdates buffers deduplicated hints inside the actual SqlStore transaction, dispatches only after commit and transaction disposal, and discards rollback state. Repeated notification inserts return no new hint; use `OUTPUT inserted.id` rather than affected-row assumptions. Support mutations and read states use the same commit discipline. Dispatch failures cannot undo a persisted business operation.
- SignalR carries opaque `Changed` invalidation hints only; load canonical notification lists/counts and conversations via authorized HTTP APIs. Preserve message ordering and stable-ID deduplication, rejoin on reconnect, pause hidden tabs, coalesce bursts and retain bounded retries plus polling fallback. No transactional outbox, new SQL table, migration or historical backfill is required.
- Use standard WebSocket/SSE/LongPolling negotiation. The Next HTTP proxy supports streaming and SignalR DELETE cleanup; direct IIS WebSocket routing needs separate future server approval. Never change IIS/CDN configuration or assume WebSocket availability. Current delivery is single-process; scale-out/backplanes require a separate design.
- Tests use the existing memory API host, mocked SMS, real local WebSocket protocol probes and browser transport/fallback scenarios. Do not write to operational SQL, send SMS/Push, deploy, publish or regenerate release ZIPs for this implementation task. See `docs/SIGNALR-REALTIME-FA.md`.

### Final unified UAT (2026-11-18)

The final product specification supersedes historical notes describing optional original PDFs, archived support, ordinary re-pricing after acceptance and notification deep-link activation. Preserve legacy financial snapshots. The active discount is configured from the print catalog page with an exclusive threshold and integer percentage (initially more than 50 copies, 10%). Printing requires explicit file review, an accepted quote, a current owner-accepted version of nonfinancial delivery conditions, and no pending correction. Support messages have independent persisted read state. Notification entries are text with read controls. The print-shop settings screen is outside this UAT. See acceptance evidence.


## 24. Owner-approved operational workflow

For CHAP GITI operations, carry an existing approval through preflight, the authorized operation, targeted verification, full regression, fixes for regressions caused by that change, repeat verification, and a final report. Routine backups, checksum verification, local startup and tests within the approved scope do not require renewed owner approval.

Full regression covers backend/API and integration suites, frontend units, browser E2E, architecture, typecheck, backend build, production-safe frontend build and lint, and migration/source contracts. Include real SQL within the specific authorization and WebSocket/SignalR for realtime work. Distinguish pre-existing warnings, failures and unexecuted checks. Do not mark unexecuted checks as passed.

Stop for an unexpected target or migration, failed backup verification, unexpected destructive operations or data loss, unapproved changes to existing data or additional main-database migrations, missing access/secrets, out-of-scope production/infrastructure changes, or a new product decision. Release or deploy only with task-specific approval. If publication is authorized, verify it; otherwise finish after the report.

## 25. Request, financial proposal and final delivery agreement (2026-11-19)

For new order work use the final workflow and UAT checklist. Sending a quote or final delivery terms reviews the exact current PDF in the same command transaction. New correction requests require a reason, instructions and a replacement PDF. Opening the order does not approve a file. Keep older file versions and decisions readable.

A financial proposal contains the numeric toman amount and included scope. Owner acceptance locks the financial proposal. Issue delivery conditions separately, bind them to the quote, specifications and file, and require explicit customer acceptance of the current version. A requested amendment invalidates that version. Production remains an explicit guarded administrator command. Continue to exclude the printshop settings page from the UAT redesign.

### Customer workflow and operational boundaries

- Preserve the eight approved customer estimate rows and the full internal pricing snapshot. A confirmed cut tariff with no numerical changes needs no new revision.
- The notification bell contains the paginated inbox. Keep per-recipient persistence, read acknowledgements, SignalR, polling and menu badges.
- The authenticated owner can continue the open conversation for their order. Replies do not implicitly request a customer action. Ticket attachments stay in private storage and never approve or replace an order PDF.
- Use the locked server version for order actions. Replacing pending or previously accepted final terms requires an explicit revision and fresh customer consent. Retain previous consent and all file/quote histories.
- User-facing errors are Persian. Preserve typed codes, correlation IDs and allowlisted private rolling diagnostics. Keep secrets, private filenames, payloads and customer identifiers out of logs.
- Preview frontend changes at desktop, tablet and phone sizes. Run backend, SQL isolation, architecture, TypeScript, lint and browser verification before a development-branch push. A source push is independent of server deployment.

- Customer ticket intake and later messages share attachment validation, private storage, ownership, ordered replay checks and rollback. Verify both general and order-linked creation with a real isolated SQL browser journey. Keep multipart requests and text-only JSON compatible.
