import { ensureSession } from "./session-client";
import { startRealtime, type RealtimeState } from "./realtime-connection";

export function connectRealtime(scope: "customer" | "admin", kind: "notifications" | "ticket",
  changed: () => void, state: (value: RealtimeState) => void, ticketId?: string) {
  return startRealtime(kind, {
    create: async () => {
      // Loaded only in the three authenticated areas. Never require a CDN or read HttpOnly cookies.
      const signalR = await import("@microsoft/signalr");
      return new signalR.HubConnectionBuilder()
        .withUrl("/api/realtime/" + scope + "/" + (kind === "ticket" ? "support" : "notifications"), { withCredentials: true, timeout: 10000 })
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .configureLogging(signalR.LogLevel.None).build();
    },
    ready: () => ensureSession(scope === "admin" ? "administrator" : "customer"),
    visible: () => document.visibilityState === "visible",
    schedule: (callback, delay) => window.setTimeout(callback, delay), cancel: timer => window.clearTimeout(timer as number),
    listen: (event, callback) => {
      const target = event === "visibilitychange" ? document : window;
      target.addEventListener(event, callback); return () => target.removeEventListener(event, callback);
    }, changed, state,
  }, ticketId);
}
