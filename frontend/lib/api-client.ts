import { responseData } from "./api-error";
import { redirectToCustomerLogin } from "./customer-client";
import { sessionFetch } from "./session-client";

export async function api<T = unknown>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await sessionFetch("/api/" + path, {
    cache: "no-store",
    ...options,
    headers: { ...(options.body instanceof FormData ? {} : { "Content-Type": "application/json" }), ...options.headers },
  });
  if (response.status === 401) {
    if (path.startsWith("admin/")) window.location.replace("/admin/login?next=" + encodeURIComponent(location.pathname + location.search));
    else if (!path.startsWith("auth/")) redirectToCustomerLogin();
  }
  return responseData(response) as Promise<T>;
}
