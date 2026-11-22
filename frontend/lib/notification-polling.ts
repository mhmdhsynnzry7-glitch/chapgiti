export type RefreshReason = "initial" | "poll" | "focus" | "mutation";
type PollEnvironment = {
  visible: () => boolean;
  schedule: (callback: () => void, delay: number) => unknown;
  cancel: (timer: unknown) => void;
  interval?: () => number;
  listen: (event: "focus" | "visibilitychange" | "online" | "mutation", callback: () => void) => () => void;
};

// A single serial HTTP reconciliation loop. Real-time hints share its coalescing path.
export function startNotificationPolling(refresh: (reason: RefreshReason) => Promise<boolean>, env: PollEnvironment) {
  let stopped = false, inFlight = false, pendingMutation = false, timer: unknown;
  const cancel = () => { if (timer !== undefined) env.cancel(timer); timer = undefined; };
  async function trigger(reason: RefreshReason) {
    cancel();
    if (stopped || !env.visible()) return;
    if (inFlight) { if (reason === "mutation") pendingMutation = true; return; }
    inFlight = true;
    let canPoll = true;
    try { canPoll = await refresh(reason); } catch { /* A temporary failure never affects business actions. */ }
    finally {
      inFlight = false;
      if (!stopped && env.visible()) {
        if (pendingMutation) { pendingMutation = false; void trigger("mutation"); }
        else if (canPoll) timer = env.schedule(() => { void trigger("poll"); }, env.interval?.() ?? 20_000);
      }
    }
  }
  const disposers = [env.listen("focus", () => { void trigger("focus"); }),
    env.listen("visibilitychange", () => { if (env.visible()) void trigger("focus"); else { pendingMutation = false; cancel(); } }),
    env.listen("online", () => { void trigger("focus"); }),
    env.listen("mutation", () => { void trigger("mutation"); })];
  void trigger("initial");
  return () => { stopped = true; cancel(); disposers.forEach(dispose => dispose()); };
}
