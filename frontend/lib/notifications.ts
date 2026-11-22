export type NotificationScope = "customer" | "admin";
export type NotificationItem = {
  orderCode?:string | null;
  id: string; eventType: string; entityKind: "order" | "ticket"; entityId: string;
  title: string; body: string; targetPath: string; createdAt: string; readAt: string | null;
};
export type NotificationPage = { items: NotificationItem[]; total: number; page: number; pageSize: number };
export type NotificationSummary = { unreadCount: number; latestSequence: number; unseenOrderCount?: number; unreadSupportCount?: number; orderViewsAvailable?: boolean };

// Reject external, cross-context, encoded, query-string and arbitrary application links.
export function notificationTarget(path: string, scope: NotificationScope): string | null {
  const prefix = scope === "admin" ? "(?:/admin/orders|/admin/tickets)" : "(?:/account/orders|/tickets)";
  return new RegExp("^" + prefix + "/[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$", "i").test(path) ? path : null;
}

export const notificationTime = (value: string) => new Date(value).toLocaleString("fa-IR", {
  timeZone: "Asia/Tehran", dateStyle: "medium", timeStyle: "short",
});
