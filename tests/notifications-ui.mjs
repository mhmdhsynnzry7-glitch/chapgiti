import { submitPrintRequest } from "./pdf-fixture.mjs";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";

// Called only by the isolated memory/guarded SQL harnesses, never a configured operational host.
export async function notificationChecks({ browser, page: admin, base, check, customerPhone = "09000000826", password = "Old.Test.Password@1",control }) {
  mkdirSync(".qa/notifications", { recursive: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await context.route("**/*", route => route.request().url().startsWith(base) ? route.continue() : route.abort());
  // This existing suite verifies the persistent HTTP/polling fallback. Live
  // delivery and transport negotiation are exercised by realtime-ui.mjs.
  await context.route("**/api/realtime/**", route => route.fulfill({status:503,json:{error:"isolated realtime outage"}}));
  const customer = await context.newPage(), errors = [];
  customer.on("pageerror", error => errors.push(error.message));
  const center = target => target.locator(".notification-center");
  const bell = target => target.getByRole("button", { name: /^اعلان‌ها/ });
  async function api(target, method, route, body) {
    return target.evaluate(async ({ method, route, body }) => {
      const response = await fetch("/api/" + route, { method, headers: { "Content-Type": "application/json" }, ...(body ? { body: JSON.stringify(body) } : {}) });
      return { status: response.status, body: await response.json() };
    }, { method, route, body });
  }
  async function until(read, predicate, label) {
    for (let n = 0; n < 80; n++) { const value = await read(); if (predicate(value)) return value; await new Promise(resolve => setTimeout(resolve, 100)); }
    assert.fail(label);
  }
  async function ready(target) {
    await center(target).waitFor();
    await until(() => center(target).innerText(), text => !text.includes("در حال دریافت اعلان‌ها"), "notification list finishes loading");
  }
  async function summary(target, scope) { const result = await api(target, "GET", scope + "/notifications/unread-count"); assert.equal(result.status, 200); return result.body; }
  async function notice(target, scope, entityId, eventType) {
    const result = await api(target, "GET", scope + "/notifications?pageSize=50"); assert.equal(result.status, 200);
    const item = result.body.items.find(row => row.entityId === entityId && row.eventType === eventType); assert.ok(item, eventType + " exists"); return item;
  }
  const spec = { pages: 120, quantity: 1, size: "raghi", paper: "white", color: "bw", binding: "soft" };
  try {
    await customer.goto(base + "/");
    check(await customer.locator(".notification-bell").count() === 0, "guest header does not fabricate a customer notification inbox");
    await customer.goto(base + "/account/notifications"); await customer.locator("input[name=phone]").waitFor();
    check(new URL(customer.url()).searchParams.get("next") === "/account/notifications", "notification page requires customer login and preserves its safe return route");
    await customer.locator("input[name=phone]").fill(customerPhone); await customer.locator("input[name=password]").fill(password);
    await customer.locator("form").getByRole("button", { name: "ورود", exact: true }).click(); await customer.waitForURL("**/account/notifications"); await ready(customer);
    check(await bell(customer).isVisible(), "authenticated customer header exposes the independent notification bell");
    const empty = async route => route.fulfill({ status: 200, json: { items: [], total: 0, page: 1, pageSize: 20 } });
    await customer.route("**/api/customer/notifications?*", empty); await customer.reload(); await ready(customer);
    check(await center(customer).getByText("هنوز اعلانی ندارید.", { exact: true }).isVisible(), "notification center has a truthful empty state");
    await customer.unroute("**/api/customer/notifications?*", empty);
    const offline = async route => route.fulfill({ status: 503, json: { error: "isolated offline response" } });
    await customer.route("**/api/customer/notifications**", offline); await customer.reload(); await ready(customer);
    check(await center(customer).getByRole("button", { name: "تلاش دوباره", exact: true }).isVisible(), "notification GET failure shows a recoverable error and retry action");
    const key = crypto.randomUUID(), creation = { idempotencyKey: key, title: "Notification browser request", spec };
    const created = await submitPrintRequest(customer, base, creation); assert.equal(created.status, 201); const id = created.body.id;
    check(created.status === 201, "failed notification reads do not disable authenticated request creation");
    await customer.unroute("**/api/customer/notifications**", offline);
    await center(customer).getByRole("button", { name: "تلاش دوباره", exact: true }).click(); await ready(customer);
    check(!(await center(customer).innerText()).includes("دریافت یا ذخیرهٔ اعلان انجام نشد"), "retry recovers notification reads without reloading business state");
    await admin.goto(base + "/admin/notifications"); await ready(admin);
    const newNotice = await notice(admin, "admin", id, "order.created");
    check(await center(admin).locator('[data-notification-id="' + newNotice.id + '"]').isVisible(), "committed request appears in the persistent administrator inbox");
    const beforeOpen = (await summary(admin, "admin")).unreadCount;
    await bell(admin).click(); await admin.locator(".notification-popover .notification-item").first().waitFor();
    check(await admin.locator(".notification-popover").getByRole("link", { name: "مشاهده همه", exact: true }).isVisible() && (await summary(admin, "admin")).unreadCount === beforeOpen, "opening short dropdown exposes view-all and leaves unread states unchanged");
    await admin.keyboard.press("Escape");
    check(await bell(admin).evaluate(element => element === document.activeElement), "Escape closes notification dropdown and restores keyboard focus");
    const openRequest = center(admin).locator('[data-notification-id="' + newNotice.id + '"] .notification-read');
    const readResponse = admin.waitForResponse(response => response.url().endsWith("/" + newNotice.id + "/read") && response.request().method() === "POST");
    await openRequest.click(); assert.equal((await readResponse).status(), 200); assert.equal(new URL(admin.url()).pathname, "/admin/notifications"); await admin.goto(base + "/admin/orders/" + id);
    await admin.getByRole("button", { name: "ارسال پیشنهاد قیمت", exact: true }).waitFor();
    check(true, "marking an administrator notification read preserves location; order details open separately");
    const detail = (await api(admin, "GET", "admin/orders/" + id)).body;
    const send = { idempotencyKey: crypto.randomUUID(), expectedVersion: detail.order.version, amountToman: 280000, scopeText: "Notification browser quoted scope", deliveryTerms: "Collect at print shop", exclusionsText: "" };
    assert.equal((await api(admin, "POST", "admin/orders/" + id + "/quotes", send)).status, 200);
    await api(admin, "POST", "admin/orders/" + id + "/quotes", send);
    const quoteNotice = await notice(customer, "customer", id, "quote.issued");
    await customer.clock.install();
    const polled = customer.waitForResponse(response => response.url().includes("/api/customer/notifications/unread-count"));
    await customer.clock.fastForward(21_000); await polled;
    await center(customer).locator('[data-notification-id="' + quoteNotice.id + '"]').waitFor();
    check(await bell(customer).locator(".notification-badge").isVisible(), "20-second HTTP fallback polling updates owner inbox and unread badge without a page reload");
    const list = (await api(customer, "GET", "customer/notifications?pageSize=50")).body.items;
    check(list.filter(row => row.entityId === id && row.eventType === "quote.issued").length === 1, "repeated proposal command produces one visible notification");
    let polls = 0; const countPoll = request => { if (request.url().includes("/api/customer/notifications/unread-count")) polls++; };
    customer.on("request", countPoll);
    await customer.evaluate(() => { window.__notificationHidden = true; Object.defineProperty(document, "visibilityState", { configurable: true, get: () => window.__notificationHidden ? "hidden" : "visible" }); document.dispatchEvent(new Event("visibilitychange")); });
    const atHidden = polls; await customer.clock.fastForward(65_000);
    check(polls === atHidden, "hidden tabs make no notification polling requests");
    const focusResponse = customer.waitForResponse(response => response.url().includes("/api/customer/notifications/unread-count"));
    await customer.evaluate(() => { window.__notificationHidden = false; document.dispatchEvent(new Event("visibilitychange")); window.dispatchEvent(new Event("focus")); }); await focusResponse;
    check(polls === atHidden + 1, "visible focus return refreshes once without catch-up requests or toast flooding");
    customer.off("request", countPoll);
    await ready(customer);
    const failedRead = async route => route.fulfill({ status: 500, json: { error: "isolated read failure" } });
    await customer.route("**/api/customer/notifications/*/read", failedRead);
    await center(customer).locator('[data-notification-id="' + quoteNotice.id + '"] .notification-read').click();
    await center(customer).getByRole("alert").waitFor();
    check(new URL(customer.url()).pathname === "/account/notifications" && (await notice(customer, "customer", id, "quote.issued")).readAt === null, "failed read-one cannot navigate or falsely mark the notification read");
    await customer.unroute("**/api/customer/notifications/*/read", failedRead);
    await center(customer).locator('[data-notification-id="' + quoteNotice.id + '"] .notification-read').click(); await until(() => notice(customer, "customer", id, "quote.issued"), row => !!row.readAt, "read is persisted");
    assert.equal(new URL(customer.url()).pathname, "/account/notifications"); await customer.goto(base + "/account/orders/" + id);
    await customer.getByRole("button", { name: "تأیید قیمت", exact: true }).waitFor();
    check(true, "customer can separately open their quote after reading its notification");
    customer.once("dialog", dialog => dialog.accept()); await customer.getByRole("button", { name: "تأیید قیمت", exact: true }).click();
    await customer.locator(".customer-quote-actions:visible").waitFor({ state: "hidden" });
    check((await notice(admin, "admin", id, "quote.accepted")).targetPath === "/admin/orders/" + id, "customer approval notifies the administrator without starting production");
    const detailPath = "admin/orders/" + id;
    const latest = async () => (await api(admin, "GET", detailPath)).body;
    const accepted = await latest();
    const fileId = accepted.files[0].id;
    assert.equal((await api(admin, "POST", detailPath + "/files/approve", {fileId, expectedVersion:accepted.order.version, idempotencyKey:crypto.randomUUID()})).status, 200);
    const termsId = crypto.randomUUID();
    assert.equal((await api(admin, "POST", detailPath + "/delivery-terms", {delivery:"Collect in person", scope:"Printing", expectedVersion:(await latest()).order.version, idempotencyKey:termsId})).status, 200);
    assert.equal((await api(customer, "POST", "customer/orders/" + id + "/delivery-terms/accept", {termsId, expectedVersion:(await latest()).order.version, idempotencyKey:crypto.randomUUID()})).status, 200);
    await admin.reload(); await admin.getByRole("button", { name: "شروع چاپ", exact: true }).waitFor();
    let actionRefreshes = 0; const afterAction = request => { if (request.url().includes("/api/admin/notifications/unread-count")) actionRefreshes++; };
    admin.on("request", afterAction); admin.once("dialog", dialog => dialog.accept()); await admin.getByRole("button", { name: "شروع چاپ", exact: true }).click();
    await admin.getByRole("button", { name: "پایان چاپ و ارسال به صحافی", exact: true }).waitFor();
    await until(() => Promise.resolve(actionRefreshes), value => value > 0, "successful business action triggers notification refresh"); admin.off("request", afterAction);
    check(actionRefreshes > 0, "successful administrator actions trigger a notification refresh independently of the poll timer");
    const printingNotice = await notice(customer, "customer", id, "order.printing");
    await customer.goto(base + "/account/notifications"); await ready(customer);
    await center(customer).locator('[data-notification-id="' + printingNotice.id + '"] .notification-read').click();
    await until(() => notice(customer, "customer", id, "order.printing"), row => !!row.readAt, "read-one persists");
    await customer.reload(); await ready(customer);
    check(await center(customer).locator('[data-notification-id="' + printingNotice.id + '"].is-read').isVisible(), "read-without-opening survives refresh with a persistent read timestamp");
    const {archiveTicketId:ticket}=await control({seedArchivedTicketFor:customerPhone});
    const archiveNotice=await notice(customer,"customer",ticket,"ticket.message");
    await customer.goto(base+"/account/notifications");await ready(customer);
    await center(customer).locator('[data-notification-id="'+archiveNotice.id+'"] .notification-open').click();
    assert.equal(new URL(customer.url()).pathname, "/account/notifications");
    await customer.goto(base+"/tickets/"+ticket);await customer.locator(".ticket-thread").waitFor();
    check(await customer.locator('.ticket-bubble.admin').count()===1,"prior support messages are accessible by opening the authorized thread");
    const paginationOrder=(await submitPrintRequest(customer,base,{idempotencyKey:crypto.randomUUID(),title:"Notification pagination",spec})).body.id;
    await api(admin,"POST","admin/orders/"+paginationOrder+"/open",{});
    for(let n=0;n<21;n++){
      const state=(await api(admin,"GET","admin/orders/"+paginationOrder)).body;
      assert.equal((await api(admin,"POST","admin/orders/"+paginationOrder+"/quotes",{idempotencyKey:crypto.randomUUID(),expectedVersion:state.order.version,amountToman:100000+n,scopeText:"Synthetic quote revision",deliveryTerms:"",exclusionsText:""})).status,200);
    }
    await customer.goto(base + "/account/notifications"); await ready(customer);
    check(await center(customer).locator(".notification-item").count() === 20, "notification center uses server pagination with twenty entries per page");
    const firstIds = await center(customer).locator(".notification-item").evaluateAll(items => items.map(item => item.dataset.notificationId));
    await center(customer).getByRole("button", { name: "صفحه بعد", exact: true }).click(); await ready(customer);
    const secondIds = await center(customer).locator(".notification-item").evaluateAll(items => items.map(item => item.dataset.notificationId));
    check(secondIds.length > 0 && !secondIds.some(id => firstIds.includes(id)), "RTL next-page action fetches a distinct persistent page without duplicates");
    const sibling = await context.newPage(); sibling.on("pageerror", error => errors.push(error.message));
    await sibling.goto(base + "/account/notifications"); await ready(sibling);
    await until(() => sibling.locator(".notification-badge").count(), value => value === 1, "second tab loads unread badge");
    const administratorUnread = (await summary(admin, "admin")).unreadCount;
    await center(customer).getByRole("button", { name: "خواندن همه", exact: true }).click();
    await until(() => summary(customer, "customer"), value => value.unreadCount === 0, "mark-all persisted");
    await until(() => sibling.locator(".notification-badge").count(), value => value === 0, "read-all refreshes another tab");
    check((await summary(admin, "admin")).unreadCount === administratorUnread, "mark-all persists across tabs while leaving the administrator inbox independent");
    await sibling.reload(); await ready(sibling);
    check(await center(sibling).locator(".notification-item.is-unread").count() === 0, "read-all state survives refresh in a separate tab");
    await sibling.close();
    for (const [target, scope] of [[customer, "customer"], [admin, "admin"]]) {
      await target.goto(base + (scope === "customer" ? "/account/notifications" : "/admin/notifications")); await ready(target);
      await target.addScriptTag({ path: "frontend/node_modules/axe-core/axe.min.js" });
      const violations = await target.evaluate(async () => (await window.axe.run(".notification-center", { runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"] } })).violations.map(row => ({ id: row.id, targets: row.nodes.map(node => node.target) })));
      assert.deepEqual(violations, []); check(true, scope + " notification center passes automated accessibility checks");
      await target.screenshot({ path: ".qa/notifications/" + scope + "-desktop.png", fullPage: true, animations: "disabled" });
      for (const width of [360, 768, 1440]) {
        await target.setViewportSize({ width, height: 900 });
        check(await target.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), scope + " notification layout avoids horizontal overflow at " + width + "px");
      }
      await target.setViewportSize({ width: 360, height: 844 }); await bell(target).click(); await target.locator(".notification-popover .notification-item").first().waitFor();
      const box = await target.locator(".notification-popover").boundingBox();
      check(box.x >= 0 && box.x + box.width <= 361, scope + " dropdown fits a small RTL mobile viewport");
      await target.addScriptTag({ path: "frontend/node_modules/axe-core/axe.min.js" });
      const popupViolations = await target.evaluate(async () => (await window.axe.run(".notification-popover", { runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"] } })).violations.map(row => row.id));
      assert.deepEqual(popupViolations, []); check(true, scope + " mobile dropdown has accessible controls and readable contrast");
      await target.screenshot({ path: ".qa/notifications/" + scope + "-mobile.png", fullPage: true, animations: "disabled" }); await target.keyboard.press("Escape"); await target.setViewportSize({ width: 1440, height: 1000 });
    }
    const forbidden = async route => route.fulfill({ status: 403, json: { error: "Isolated forbidden scope" } });
    await customer.route("**/api/customer/notifications**", forbidden); await customer.reload(); await ready(customer);
    check(await center(customer).getByRole("link", { name: "ورود دوباره", exact: true }).isVisible() && await customer.locator(".notification-badge").count() === 0, "403 clears stale private unread data and offers the correct customer login");
    await customer.unroute("**/api/customer/notifications**", forbidden);
    await api(customer, "POST", "customer/logout");
    await customer.goto(base + "/customer?change=1&next=/account/notifications"); await customer.locator("input[name=phone]").fill(customerPhone); await customer.locator("input[name=password]").fill(password);
    await customer.locator("form").getByRole("button", { name: "ورود", exact: true }).click(); await customer.waitForURL("**/account/notifications"); await ready(customer);
    check(await center(customer).locator(".notification-item.is-unread").count() === 0, "persistent read states remain after logout and a new customer login");
    check(errors.length === 0, "notification browser journeys have no runtime JavaScript errors");
  } finally {
    await admin.setViewportSize({ width: 1440, height: 1000 });
    await context.close();
  }
}
