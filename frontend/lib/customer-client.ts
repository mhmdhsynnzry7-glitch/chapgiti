import { responseData } from "./api-error";
import { sessionFetch } from "@/lib/session-client";
export function redirectToCustomerLogin() {
  window.location.replace("/customer?next=" + encodeURIComponent(window.location.pathname + window.location.search + window.location.hash));
}
export async function customerApi(path: string, options: RequestInit = {}) {
  const response = await sessionFetch("/api/" + path, { ...options, headers: { ...(options.body instanceof FormData ? {} : {"Content-Type": "application/json"}), ...options.headers } });
  if (response.status === 401) redirectToCustomerLogin();
  return responseData(response);
}
