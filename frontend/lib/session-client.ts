export type SessionContext = "customer" | "administrator";
const renewals: Record<SessionContext, Promise<boolean> | null> = { customer: null, administrator: null };

export function secureAuthenticationOrigin(origin: string): boolean {
  try {
    const url = new URL(origin);
    return url.protocol === "https:" || (url.protocol === "http:" &&
      ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname));
  } catch { return false; }
}

export function requireSecureAuthenticationPage() {
  if (typeof window !== "undefined" && !secureAuthenticationOrigin(window.location.origin))
    throw new Error("ورود، ثبت‌نام و بازیابی رمز فقط از آدرس HTTPS امن در دسترس است.");
}

async function renew(context: SessionContext): Promise<boolean> {
  const prefix = context === "administrator" ? "/api/auth" : "/api/customer";
  requireSecureAuthenticationPage();
  // Checking after acquiring the cross-tab lock avoids rotating a token another tab just renewed.
  const current = await fetch(prefix + "/session", { cache: "no-store" });
  if (current.ok) return true;
  if (current.status !== 401) return false;
  const response = await fetch(prefix + "/refresh", { method: "POST", cache: "no-store" });
  if (response.ok) return true;
  if (response.status === 409) {
    await new Promise(resolve => setTimeout(resolve, 350));
    return (await fetch(prefix + "/session", { cache: "no-store" })).ok;
  }
  return false;
}

export function ensureSession(context: SessionContext = "customer"): Promise<boolean> {
  if (!renewals[context]) {
    const work = () => renew(context).catch(() => false);
    renewals[context] = (async () => {
      if (typeof navigator !== "undefined" && navigator.locks)
        return await navigator.locks.request("chap-giti-session-renewal-" + context, work);
      return await work();
    })().finally(() => { renewals[context] = null; });
  }
  return renewals[context]!;
}

export async function sessionFetch(url: string, options: RequestInit = {}): Promise<Response> {
  async function request() {
    try {return await fetch(url, options);}
    catch (error) {
      if (options.signal?.aborted) throw error;
      throw new Error("ارتباط با سرور برقرار نشد. دوباره تلاش کنید.");
    }
  }
  let response = await request();
  if (response.status === 401 && !/\/(?:login|logout|password\/)/.test(url) &&
    await ensureSession(/^\/api\/(?:admin|auth)(?:\/|$)/.test(url) ? "administrator" : "customer")) response = await request();
  if (response.ok && !["GET", "HEAD"].includes((options.method || "GET").toUpperCase()) &&
    /^\/api\/(?:orders(?:\/|$)|tickets(?:\/|$)|contact$|customer\/orders\/|admin\/(?:orders|tickets)(?:\/|$))/.test(url) &&
    typeof window !== "undefined" && typeof window.dispatchEvent === "function") window.dispatchEvent(new Event("chap:notifications-refresh"));
  if (response.status === 409 && typeof window !== "undefined") {
    const id = url.match(/^\/api\/(?:admin|customer)\/orders\/([a-f0-9-]{36})(?:\/|$)/)?.[1];
    if (id) window.dispatchEvent(new CustomEvent("chap:order-conflict",{detail:id}));
  }
  return response;
}
