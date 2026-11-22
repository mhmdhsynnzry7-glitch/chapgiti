import assert from "node:assert/strict";
import { notificationTarget, notificationTime } from "../frontend/lib/notifications.ts";
import { startNotificationPolling } from "../frontend/lib/notification-polling.ts";

let checks = 0;
const check = (condition, name) => { assert.ok(condition, name); checks++; console.log("PASS " + name); };
const id = "ed8f66aa-24c9-4214-abce-6d9c145bf33a";
for (const [scope, paths] of [["customer", ["/account/orders/", "/tickets/"]], ["admin", ["/admin/orders/", "/admin/tickets/"]]]) {
  for (const path of paths) check(notificationTarget(path + id, scope) === path + id, scope + " internal entity target is retained");
  for (const path of ["https://evil.test/" + id, "//evil.test/" + id, "/account/orders/%2f" + id, "/account/orders/../" + id,
    "/admin/profile", "/tickets/" + id + "?next=https://evil.test", "/tickets/" + id + "#private", "/tickets\\" + id])
    check(notificationTarget(path, scope) === null, scope + " rejects unsafe or non-entity notification target");
}
check(notificationTarget("/admin/orders/" + id, "customer") === null && notificationTarget("/account/orders/" + id, "admin") === null, "notification deep links cannot cross independent authentication contexts");
check(/[۰-۹]/.test(notificationTime("2026-11-13T10:15:00Z")), "timestamps use Persian digits and the Tehran timezone");

function environment() {
  const listeners = new Map(), timers = new Map(); let next = 0;
  const state = { visible: true, listeners, timers,
    api: { visible: () => state.visible, schedule: (fn, delay) => { const id = ++next; timers.set(id, { fn, delay }); return id; },
      cancel: id => timers.delete(id), listen: (name, fn) => { listeners.set(name, fn); return () => listeners.delete(name); } },
    event(name) { listeners.get(name)?.(); }, tick() { const entry = timers.entries().next().value; assert.ok(entry, "one timer expected"); timers.delete(entry[0]); entry[1].fn(); },
  };
  return state;
}
const flush = async () => { for (let i = 0; i < 6; i++) await Promise.resolve(); };
{
  const state = environment(), calls = [];
  const stop = startNotificationPolling(async reason => { calls.push(reason); return true; }, state.api);
  await flush();
  check(calls.join() === "initial" && [...state.timers.values()][0].delay === 20_000, "notification loop starts once and schedules a lightweight 20-second poll");
  state.tick(); await flush(); check(calls.join() === "initial,poll" && state.timers.size === 1, "a completed poll schedules just one next request");
  state.visible = false; state.event("visibilitychange");
  check(state.timers.size === 0, "hidden tabs cancel polling timers");
  state.event("focus"); state.event("mutation"); await flush(); check(calls.length === 2, "background focus or actions do not trigger notification network traffic");
  state.visible = true; state.event("visibilitychange"); state.event("focus"); await flush();
  check(calls.length === 3 && calls.at(-1) === "focus", "returning to a visible tab refreshes once without catch-up requests");
  state.event("mutation"); await flush(); check(calls.at(-1) === "mutation", "successful business actions refresh the notification projection");
  stop(); check(state.timers.size === 0 && state.listeners.size === 0, "unmount removes timers and all event listeners");
}
{
  const state = environment(), calls = []; let finish;
  const stop = startNotificationPolling(reason => { calls.push(reason); return new Promise(resolve => { finish = resolve; }); }, state.api);
  state.event("focus"); state.event("mutation"); state.event("mutation");
  check(calls.length === 1, "slow notification requests never overlap with focus or mutation requests");
  finish(true); await flush();
  check(calls.join() === "initial,mutation", "changes during an in-flight request coalesce into one follow-up");
  stop(); finish(true); await flush(); check(state.timers.size === 0, "an in-flight response cannot resurrect a disposed poller");
}
{
  const state = environment(); let calls = 0, available = false;
  const stop = startNotificationPolling(async () => { calls++; return available; }, state.api); await flush();
  check(state.timers.size === 0, "expired session or pending notification migration stops periodic failure loops");
  state.event("focus"); await flush(); check(calls === 2, "focus can recheck restored credentials or newly available storage");
  available = true; state.event("mutation"); await flush();
  check(state.timers.size === 1 && calls === 3, "manual retry through the mutation signal resumes polling after storage becomes available"); stop();
}
{
  const state = environment();
  const stop = startNotificationPolling(async () => { throw new Error("offline"); }, state.api); await flush();
  check(state.timers.size === 1, "temporary offline failures retain controlled retries without breaking business actions"); stop();
}
console.log(checks + " notification unit checks passed; no network, SMS, push or database used.");
