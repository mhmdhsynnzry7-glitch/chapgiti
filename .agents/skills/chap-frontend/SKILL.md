---
name: chap-frontend
description: Build or fix CHAP GITI Next.js/React customer and admin pages, Persian RTL forms, login/register/OTP journeys, calculator summaries, order tracking, services CMS, optional banners and contact links. Use for frontend UX or state changes.
---

# CHAP Frontend Workflow

1. Read `AGENTS.md`, target route/components, API client contracts and existing tests. Confirm current code before choosing a library or pattern.
2. Follow Next.js App Router, React, strict TypeScript and existing UI/dialog components; maintain Persian RTL, Vazirmatn, responsive views, focus and keyboard accessibility. Reuse `app/globals.css`; Tailwind CSS 4 is installed but its utilities are not globally imported, so do not assume utility classes are active.
3. Map all user states: initial, loading, successful, invalid input, server failure, empty data and retry. Use explicit Persian copy rather than exposing raw backend errors.
4. Customer registration target: **mobile first → OTP → complete profile/password → registration success**. Existing mobile flows to login. Login uses mobile+password, includes a persistent sign-up link, and distinguishes nonexistent customer vs wrong password using structured backend error codes when supported.
5. Admin login is separate from customer login; username `admin`, password, distinct session/cookie behavior. Do not expose legacy admin login entry on customer login page. Admin profile edits only own email and password.
6. Password-creation fields show: `رمز عبور باید حداقل ۶ کاراکتر، شامل یک حرف بزرگ انگلیسی (A-Z) و یک کاراکتر خاص مانند ! یا @ باشد.` Backend remains authoritative.
7. The customer calculator shows the selected cut, quantity, finishing per copy, unit price before discount, discountable subtotal, quantity discount, unit price after discount and final amount. Order and tracking views show the aggregate estimate or confirmed price. Never alter pricing math to hide rows. Keep detailed admin pricing when applicable.
8. Public main navigation omits `پیگیری سفارش`; retain `/track` for historical links. Logged-in navigation exposes `سفارش‌های من`, status and direct owned details without asking again for a code/mobile. Use `formatToman` for amounts, keeping plain counts separate. The magazine is active at /blog and /admin/articles; portfolio stays retired with compatible redirects and preserved historical records. Service cards/details use saved published content with consultation as their only primary action.
9. Admin banner create/edit uses accessible modals and optional CTA fields; services use a dedicated editor with unsaved-change protection, shared image uploads, validation and clear saving feedback.
10. Test interactions for changed behavior (form submit, field validation, routing, auth transitions, modals and order displays). Run safe typecheck, lint and frontend tests; inspect scripts before executing to avoid publish steps.
11. Summarize changed components, manual checks not completed, API dependencies and test results. No deploy/publish/release packaging without a separate explicit request; the current modernization task authorizes none.

## Avoid
- Do not restore duplicate admin users/admin-accounts/messages/SMS menu entries. Keep one customer directory with read-only details, active owner-scoped support and retained historical conversations, separate customer activity/admin audit, and legacy compatible routes. Use context order actions, grouped numeric-price text input and explicit confirmation. Support links from owned orders, displays real message history and does not invent unread state. Contact stays informational with configured social links. Magazine is active with private drafts and published-only public views; portfolio stays retired; finishing cards are display-only and public footer has no admin-login link. Keep independent calculator/order features.
- Inventing new design system or state library for a small task.
- Duplicating pricing or authorization authority in the browser.
- Treating frontend redirects as the only access control.

## Current catalog and magazine policy

Printing selectors read the typed SQL catalog API; use a searchable RTL combobox with portal, bounded scrolling, keyboard/Escape/focus support and explicit loading/error/empty/unavailable-selection states. Read dimensions/units and pricing compatibility from stable codes/records, never names. Preserve selection across refetch, refresh deliberately after admin updates and keep historical snapshots readable. Restore public `/blog` and the administrator «مجله», with a dedicated article editor, safe defined content, published-only list/detail/SEO/sitemap, filters, pagination and responsive readable typography. Preserve actual dates/authors/slugs, private drafts and prompt unpublish visibility. Ticket replies and attachment uploads remain active; portfolio stays retired; no article-footer calculator/order CTA. Use existing fonts/components/tokens, no unrelated library/state framework.

## Registration acceptance contract

- Render mobile number first. Issue Console OTP only for eligible new-number registration. After successful backend OTP verification, reveal profile and password fields; do not show them earlier.
- Confirm the post-registration login action resets the form to password login, retains the normalized mobile in local state, and supports a safe internal return path.
- Keep the registration action under the login form and use structured `CUSTOMER_ACCOUNT_NOT_FOUND` versus `CUSTOMER_PASSWORD_INCORRECT` responses.
- Keep the registration grant in component memory only, never URLs/localStorage/cookies. Submit it with profile/password to `registration/complete`; handle its five-minute expiry and server rejection by offering mobile reverification. OTP verification alone is not registration success or passwordless login.
- Recovery errors use structured codes: unknown accounts stay on mobile entry with the exact registration guidance/link and retained mobile; do not start an OTP timer without a validated challenge. Incorrect codes retain verification; terminal/stale errors clear OTP/reset state and return to mobile entry with the appropriate Persian message. Registration opened from recovery must switch purpose to register and keep all approved registration steps.


## Request-to-production acceptance

Estimation and request creation both require a completed customer session. Show per-cut unit and total estimates from the authenticated API and offer sign-in on session expiry. Preserve only validated technical specs in sessionStorage for the login return, with a safe draft flag. Customer quote decisions, revision reasons, queued status and explicit admin start must stay distinct. Use role-appropriate Persian status labels and existing grouped toman formatting.

## Notification acceptance

Use one authenticated identity-keyed provider per customer/admin layout, with independent endpoints and no shared private cache. Reuse the header notification popover, RTL pagination and internal entity target allowlist. Await successful read persistence before navigating. Poll summary ~20 seconds only while visible, refresh on focus/actions, and synchronize same-scope tabs; do not generate bulk toasts. Treat 401/403, missing storage and offline reads explicitly without blocking order/support actions. No browser permission prompts, service workers or phone push.

Follow the current workflow and support rules in root AGENTS.md. Automated tests use isolated data and mocked delivery. Apply reviewed Development migrations only through the guarded project runner after target verification and a verified backup. Publish editorial content in Development only when the owner authorizes it through the existing administrator API.

- Keep eight customer estimate rows, the full profile name, Persian native validation and route-driven list/detail navigation. Test RTL focus, refresh, stale-order reconciliation and paginated notification bells at phone, tablet and desktop widths.
