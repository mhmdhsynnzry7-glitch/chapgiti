---
name: chap-testing
description: Plan, write and run proportional CHAP GITI unit, API integration, frontend, regression and architecture tests; safely validate OTP, pricing, orders, authorization and database code. Use whenever implementation or bug fixes need verification.
---

# CHAP Testing Workflow

1. Inspect `AGENTS.md`, current test framework, package scripts and affected code. **Read script bodies** before execution; never run a test/build script that triggers publish, release packaging, live SMS or operational writes.
2. Select the smallest meaningful coverage: Domain unit tests for rules; application/controller tests for use cases, mappings and authorization; component tests for critical form interactions; E2E only for high-value customer journeys when harness is available.
3. Test happy path plus relevant boundary/error cases. Every fixed bug receives a regression test reproducing its cause, not just asserting a displayed message vanished.
4. OTP: mock MelliPayamak Console transport; test provider `code`/`status` success and failure, missing/malformed response, invalid/expired/consumed OTP, purpose binding, resend/attempt limits and password recovery. Never send real SMS in automated tests.
5. Auth: test separate customer/admin sessions, independent logout/refresh, admin `admin` login, protected endpoints, current-role checks and self-profile updates. Test 6+ character, uppercase A–Z, special-character policy for new passwords and legacy-password login compatibility.
6. Pricing: test page-boundary validation, Persian/Arabic digit normalization, API shape consistency, price math and hiding breakdown only in customer UI.
7. Orders/tickets: ownership, valid/invalid state transitions, idempotency, concurrent writes, private PDF file constraints, empty listing/tracking states.
   Business regression coverage includes committed new orders in admin filters/search/pagination and focus refresh; tariff save validation, unchanged historical prices, finishing compatibility, retired portfolio redirects, published-only magazine and preserved historical records, safe customer file metadata and stored-login evidence. `node tests/money.mjs` verifies monetary formatting independently. Do not use operational writes to validate persistence.
8. Database integration tests that mutate data run **only** against a positively verified isolated disposable database. If isolation cannot be verified, skip, document why and use mocks/readonly checks. Never create/replace operational `ChapGiti` DB or modify its data without explicit authorization.
9. Run relevant safe checks and report exact command, pass/fail counts, existing warnings, skipped tests and unverified flows. Never claim an E2E or real SMS check passed from mocks alone.

## Review output template
- For explicitly authorized SQL acceptance only, use the opt-in `tests/sql005` fixture: separate LocalDB instance, synthetic database, real API/repositories, no SMS, pinned server/catalog checks and verified cleanup. Ordinary suites remain memory-only/read-only. Report engine-version differences; the 005 length regression needs a real SQL engine, not only a mock schema evaluator.
- Workflow/archive matrix: GET does not open an order, concurrent POST open transitions once, versioned proposals, owner-only acceptance/queue, explicit production start, legacy approvals, required rejection reason, conditional binding, terminal/legacy states, stale versions and rollback. Assert private-note append/replay and no public leaks, authorized historical general/owned-order ticket reads, pagination and stored message chronology. Verify authenticated intake/replies/status, recipient isolation and read acknowledgments on synthetic tickets. Use `tests/workflow-support-ui.mjs`, `tests/approved-price.mjs` and the current isolated acceptance flag.
- Preserve ordering and actual last-message preview of retained historical replies. Unknown historical order-event actors remain unknown rather than inheriting the customer's identity.
- Behavior changed:
- Tests added or updated:
- Commands actually executed and results:
- Skipped checks and reason:
- Database/SMS/server side effects: none / explicit description
- Regressions or risks remaining:

## Registration regression matrix

- Browser/API tests must prove phone-first registration, no profile form before OTP, correct/incorrect/expired/consumed OTP, verified grant required for completion, duplicate phone prevention, profile persistence in isolated tests, and successful password login after registration.
- Check both customer-login 401 codes and the two distinct Persian messages, plus the registration link retaining phone in client state. Never send real SMS or mutate operational `ChapGiti` during these tests.
- Cover `registration/complete` without a grant, wrong-purpose/mode tokens, five-minute expiry, replay, duplicate/racing completions and transaction rollback. OTP verification must leave accounts/profiles empty until completion; grant rows must not count as SMS requests. Browser checks must exercise the actual OTP → profile → completion → password-login actions.
- Recovery regression coverage includes unknown/pending/incomplete/unverified accounts before SMS/challenge creation, every structured OTP failure, stale historical client challenges, normalized mobiles, exact error copy and prefilled registration. Only eligible isolated accounts can recover; never assume neutral recovery sends are still required. When a developer's Debug API is running, use the Verification configuration for test output rather than stopping it.
- Grant tests must exercise the actual OTP repository command text against the applied schema CHECK definitions. The current isolated SQL executor evaluates these expressions in memory; all API/browser grant fixtures validate the production INSERT before accepting logical state. Reproduce historical SQL 547/HTTP 500 only in isolated tests; never re-submit a real OTP or mutate operational records to diagnose it.

## CMS V2 regression coverage

- Cover Settings.services create/edit/publish/order/version conflicts/slug aliases, original content preservation, public filtering, service metadata and consultation links to configured `/contact` information. Verify SQL persistence in the guarded disposable fixture, never operational ChapGiti; consultation follows the configured contact or general ticket entry point.
- Cover optional banner CTA, hidden historical portfolio promotions without data loss, safe configurable social links, retired portfolio navigation and published-only magazine sitemap, informational finishing cards and retained calculator options. Reuse RTL pagination tests for boundaries, search/filter resets and nonduplicated pages. Preserve auth/OTP/order/tariff regression coverage.


## Request-to-production acceptance

Cover guest and administrator-only denial, authenticated customer estimation and request creation, draft privacy/return, latest-revision owner decisions, stale/replayed/concurrent commands, rollback, historical compatibility, request-more-info/PDF, conditional binding and no private leaks. Run tests/request-draft.mjs and the --request-workflow browser branch. Write tests for 006 only in the positively verified disposable fixture; manual UAT is never inferred from automated passes.

## Notification acceptance

Use tests/notifications.mjs and tests/modernization-ui.mjs --notifications for the actual read/navigation, polling, hidden/focus, multi-tab, recovery, RTL/mobile and accessibility behaviors. API/SQL tests must cover the correct opposite recipient, independent simultaneous customer/admin sessions, immutable admin identity, event retries, ownership, read/read-all persistence and atomic rollback on notification failure. Use only synthetic messages/mock SMS and the positively guarded disposable SQL fixture for 007. Report manual UAT separately and never infer actual activation while operational migrations are pending.

When Next streaming or retained route fragments leave hidden copies in the DOM, scope order/ticket assertions to their visible region. Verify the active content and state rather than using first() to conceal duplicate matches. Preserve all business/privacy assertions.

## 008/009 SQL acceptance

Current modernization additionally requires EF mapping/baseline/history and fresh/upgrade SQL acceptance, transactional/concurrent repository parity, catalog defaults/active/pricing/history and magazine CRUD/publication/slug/content safety. Do not treat memory providers as proof of SQL compatibility. Exercise searchable selector keyboard/portal/RTL/mobile, admin catalog/editor and published-only magazine/cache/SEO, with screenshots. Magazine restoration supersedes retired-blog assertions; test active support and preserve the portfolio/footer-CTA exclusions. Never include legacy configured-database aliases in unit batches, and never run publish/package scripts for this source task.

- Current opt-in: `dotnet run --project tests/sql005/ChapGiti.SqlAcceptanceTests.csproj --configuration Verification -- --authorize-disposable-localdb-008-009`, only after explicit disposable-database authorization. Never use operational configuration or the user's working database. The default broad SQL fixture remains disabled; verify actual LocalDB server/catalog before writes/API operations and confirm cleanup.
- Exercise upgrade from 001–007 plus a fresh 001–009 chain through the real checksum/locking runner. Keep migration files/checksums intact, inspect exact SQL types/nullability/FKs/indexes/defaults and preserve synthetic historical accounts, quotes, orders, file bytes and ticket records. Report actual engine/build and distinguish SQL acceptance from memory checks and manual UAT.
- Cover file-free estimates, a mandatory initial PDF for order creation, required replacement versus old/foreign/malformed attachments, submit→pending review→approve/reject→new cycle, immutable history, approved-file selection, latest-price acceptance and all production-start routes. Add real concurrency, transaction rollback and duplicate-command regressions.
- Verify persisted opposite-recipient notifications and distinct order attention independent of read/read-all, no view from GET/list/navigation, stale-sequence safety, new login/session persistence and SignalR reconnect/HTTP resync. Test owner-scoped ticket replies, private attachment downloads and post-commit live hints. Mock SMS/OTP transports throughout.

Follow the current workflow and support rules in root AGENTS.md. Automated tests use isolated data and mocked delivery. Apply reviewed Development migrations only through the guarded project runner after target verification and a verified backup. Publish editorial content in Development only when the owner authorizes it through the existing administrator API.

- Use isolated identities for two-browser term revisions, lost responses and stale approvals. Compare persisted file metadata and bytes on replay and rollback. Exercise the actual public magazine pages after authorized Development publication.
