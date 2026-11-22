export type RealtimeState = "connecting" | "connected" | "reconnecting" | "offline";
export type RealtimeConnection = {
  start: () => Promise<void>; stop: () => Promise<void>; invoke: (method: string, ...args: string[]) => Promise<unknown>;
  on: (method: string, callback: (value: unknown) => void) => void;
  onclose: (callback: () => void) => void; onreconnecting: (callback: () => void) => void; onreconnected: (callback: () => void) => void;
};
type Environment = {
  create: () => Promise<RealtimeConnection>; ready: () => Promise<boolean>; visible: () => boolean;
  schedule: (callback: () => void, delay: number) => unknown; cancel: (timer: unknown) => void;
  listen: (event: "focus" | "online" | "visibilitychange", callback: () => void) => () => void;
  changed: () => void; state: (value: RealtimeState) => void;
};

// Lifecycle only: no personal data, persistent browser storage, tokens or business commands.
export function startRealtime(kind: "notifications" | "ticket", env: Environment, ticketId?: string) {
  let stopped = false, starting = false, generation = 0, attempts = 0;
  let current: RealtimeConnection | null = null, retry: unknown, changeTimer: unknown;
  const seen = new Set<string>();
  const cancel = () => { if (retry !== undefined) env.cancel(retry); retry = undefined; };
  function later() {
    cancel();
    if (!stopped && env.visible()) retry = env.schedule(() => { void connect(); }, [2000, 5000, 10000, 30000][Math.min(attempts++, 3)]);
  }
  async function activated(connection: RealtimeConnection) {
    if (stopped || current !== connection || !env.visible()) return;
    if (ticketId) await connection.invoke("JoinTicket", ticketId);
    if (stopped || current !== connection) return;
    attempts = 0; env.state("connected"); env.changed(); // HTTP reconciliation after every connect/reconnect.
  }
  async function connect() {
    cancel();
    if (stopped || starting || current || !env.visible()) return;
    starting = true; const version = ++generation;
    env.state("connecting");
    let connection: RealtimeConnection | undefined;
    try {
      if (!await env.ready()) throw new Error("Session unavailable");
      connection = await env.create();
      if (stopped || version !== generation || !env.visible()) { await connection.stop(); return; }
      current = connection;
      connection.on("Changed", value => {
        if (stopped || current !== connection || !env.visible() || !value || typeof value !== "object") return;
        const event = value as { kind?: unknown; eventId?: unknown };
        if (event.kind !== kind || typeof event.eventId !== "string" || !/^[a-f0-9]{64}$/.test(event.eventId) || seen.has(event.eventId)) return;
        seen.add(event.eventId); if (seen.size > 256) seen.delete(seen.values().next().value!);
        if (changeTimer === undefined) changeTimer = env.schedule(() => { changeTimer = undefined; if (!stopped && env.visible()) env.changed(); }, 75);
      });
      connection.onreconnecting(() => {
        if (current !== connection || stopped) return;
        env.state("reconnecting"); void env.ready().catch(() => false);
      });
      connection.onreconnected(() => { void activated(connection!).catch(() => {
        if (!stopped && current === connection) { disconnect(); later(); }
      }); });
      connection.onclose(() => {
        if (current !== connection || stopped) return;
        current = null; env.state("offline"); later();
      });
      await connection.start(); await activated(connection);
    } catch {
      if (current === connection) current = null;
      if (connection) await connection.stop().catch(() => {});
      if (!stopped) env.state("offline");
    } finally {
      starting = false;
      if (!stopped && !current) later();
    }
  }
  function disconnect() {
    generation++; cancel();
    if (changeTimer !== undefined) env.cancel(changeTimer); changeTimer = undefined;
    const previous = current; current = null;
    if (previous) void previous.stop().catch(() => {});
    if (!stopped) env.state("offline");
  }
  const resume = () => { if (env.visible()) { if (current) env.changed(); else void connect(); } else disconnect(); };
  const disposers = [env.listen("focus", resume), env.listen("online", resume), env.listen("visibilitychange", resume)];
  void connect();
  return () => { stopped = true; disconnect(); disposers.forEach(dispose => dispose()); seen.clear(); };
}

export function uniqueById<T extends { id: string }>(items: T[]): T[] {
  const ids = new Set<string>();
  return items.filter(item => { if (ids.has(item.id)) return false; ids.add(item.id); return true; });
}
