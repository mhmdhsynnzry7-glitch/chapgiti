import assert from "node:assert/strict";
import { ensureSession, sessionFetch, secureAuthenticationOrigin, requireSecureAuthenticationPage } from "../frontend/lib/session-client.ts";

let checks = 0;
const check = (condition, name) => { assert.ok(condition, name); checks++; console.log("PASS " + name); };
check(secureAuthenticationOrigin("https://printing.example:8443"), "HTTPS authentication origin is accepted");
check(!secureAuthenticationOrigin("http://printing.example:8081"), "public HTTP authentication is refused");
check(!secureAuthenticationOrigin("file:///account") && !secureAuthenticationOrigin("not-an-origin"), "invalid authentication origins fail closed");
check(secureAuthenticationOrigin("http://127.0.0.1:3000") && secureAuthenticationOrigin("http://localhost:3000"), "browser loopback development origins are recognized");

const previousFetch = globalThis.fetch, previousWindow = globalThis.window;
try {
  globalThis.window = { location: { origin: "http://printing.example:8081" } };
  assert.throws(requireSecureAuthenticationPage, /HTTPS/);
  check(true, "sensitive forms refuse HTTP before making a network request");
  globalThis.window = { location: { origin: "http://127.0.0.1:3000" } };
  const active = { customer: false, administrator: false }, rotations = { customer: 0, administrator: 0 };
  globalThis.fetch = async url => {
    const context = /^\/api\/(?:admin|auth)(?:\/|$)/.test(url) ? "administrator" : "customer";
    if (url.endsWith("/session")) return Response.json({}, { status: active[context] ? 200 : 401 });
    if (url.endsWith("/refresh")) {
      rotations[context]++;
      await new Promise(resolve => setTimeout(resolve, 10));
      active[context] = true; return Response.json({ ok: true });
    }
    return Response.json({}, { status: active[context] ? 200 : 401 });
  };
  const responses = await Promise.all([sessionFetch("/api/admin/users"), sessionFetch("/api/admin/profile"), sessionFetch("/api/customer/me"), sessionFetch("/api/quote")]);
  check(responses.every(response => response.ok) && rotations.customer === 1 && rotations.administrator === 1, "concurrent operations coordinate one refresh per independent authentication context");
  check(await ensureSession() && await ensureSession("administrator") && rotations.customer === 1 && rotations.administrator === 1, "already-renewed credentials do not rotate either context again");
  active.customer = false;
  await sessionFetch("/api/customer/login");
  check(rotations.customer === 1, "wrong-password responses do not cause renewal");
  active.administrator = false;
  await sessionFetch("/api/auth/login");
  check(rotations.administrator === 1, "administrator password errors do not cause customer or administrator renewal");
  await ensureSession("administrator");
  check(!active.customer && active.administrator && rotations.customer === 1, "administrator renewal cannot renew the customer context");
  const notificationSignals = [];
  globalThis.window.dispatchEvent = event => { notificationSignals.push(event.type); return true; };
  globalThis.fetch = async () => Response.json({ ok: true });
  await sessionFetch("/api/admin/orders/example/quotes", { method: "POST" });
  await sessionFetch("/api/tickets/example/messages", { method: "post" });
  check(notificationSignals.length === 2 && notificationSignals.every(name => name === "chap:notifications-refresh"), "successful order and ticket mutations signal an independent notification refresh");
  await sessionFetch("/api/customer/notifications/read-all", { method: "POST" });
  await sessionFetch("/api/admin/orders/example");
  await sessionFetch("/api/customer/login", { method: "POST" });
  check(notificationSignals.length === 2, "notification read writes, business GETs and authentication cannot create refresh feedback loops");
  globalThis.fetch = async () => Response.json({}, { status: 409 });
  await sessionFetch("/api/orders", { method: "POST" });
  check(notificationSignals.length === 2, "rejected business changes do not signal a successful notification event");
  globalThis.fetch = async () => Response.json({}, { status: 401 });
  check(!(await ensureSession()) && !(await ensureSession("administrator")), "revoked credentials fail closed independently");
} finally {
  globalThis.fetch = previousFetch;
  if (previousWindow === undefined) delete globalThis.window; else globalThis.window = previousWindow;
}
console.log(checks + " browser session unit checks passed without network access.");
