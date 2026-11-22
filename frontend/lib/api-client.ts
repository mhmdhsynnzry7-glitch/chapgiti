import { redirectToCustomerLogin } from "./customer-client";
import { sessionFetch } from "./session-client";

export async function api<T = unknown>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await sessionFetch("/api/" + path, {
    cache: "no-store",
    ...options,
    headers: { ...(options.body instanceof FormData ? {} : { "Content-Type": "application/json" }), ...options.headers },
  });
  const data: unknown = await response.json();
  if (response.status === 401) {
    if (path.startsWith("admin/")) window.location.assign("/admin/login?next=" + encodeURIComponent(location.pathname + location.search));
    else if (!path.startsWith("auth/")) redirectToCustomerLogin();
  }
  if (!response.ok) {
    const message = data && typeof data === "object" && "error" in data ? data.error : null;
    throw new Error(typeof message === "string" ? message : "درخواست انجام نشد.");
  }
  return data as T;
}
