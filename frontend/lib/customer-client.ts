import { sessionFetch } from "@/lib/session-client";
export function redirectToCustomerLogin() {
  window.location.assign("/customer?next=" + encodeURIComponent(window.location.pathname + window.location.search + window.location.hash));
}
export async function customerApi(path: string, options: RequestInit = {}) {
  const response = await sessionFetch("/api/" + path, { ...options, headers: { "Content-Type": "application/json", ...options.headers } });
  const data = await response.json();
  if (response.status === 401) redirectToCustomerLogin();
  if (!response.ok) throw new Error(data.error || "درخواست انجام نشد.");
  return data;
}
