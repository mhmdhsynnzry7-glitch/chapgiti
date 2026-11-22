"use client";

import Link from "next/link";
import { createContext, useCallback, useContext, useEffect, useId, useRef, useState, type ReactNode } from "react";
import { Bell, Check, CheckCheck } from "lucide-react";
import { Popover, PopoverContent, PopoverTrigger } from "./ui/popover";
import Pagination from "./pagination";
import { sessionFetch } from "@/lib/session-client";
import { notificationTime, type NotificationItem, type NotificationPage, type NotificationScope, type NotificationSummary } from "@/lib/notifications";
import { startNotificationPolling } from "@/lib/notification-polling";
import { connectRealtime } from "@/lib/realtime-client";
import { uniqueById, type RealtimeState } from "@/lib/realtime-connection";

class NotificationError extends Error {
  constructor(public status: number, public code: string) {
    super(status === 401 || status === 403 ? "نشست این حساب در دسترس نیست؛ دوباره وارد شوید."
      : code === "NOTIFICATIONS_UNAVAILABLE" ? "مرکز اعلان‌ها هنوز روی این سرور فعال نشده است."
      : status === 404 ? "این اعلان دیگر در دسترس نیست؛ فهرست را تازه‌سازی کنید."
      : "دریافت یا ذخیرهٔ اعلان انجام نشد. اتصال را بررسی و دوباره تلاش کنید.");
  }
}
async function request<T>(path: string, method = "GET", signal?: AbortSignal): Promise<T> {
  const timeout = AbortSignal.timeout(12_000);
  const response = await sessionFetch(path, { method, cache: "no-store", signal: signal ? AbortSignal.any([signal, timeout]) : timeout });
  const data = await response.json();
  if (!response.ok) throw new NotificationError(response.status, data.code || "");
  return data as T;
}
type Center = {
  scope: NotificationScope; base: string; summary: NotificationSummary | null; error: string; expired: boolean;
  revision: number; refresh: () => void; read: (id?: string) => Promise<void>; busy: boolean; realtimeState: RealtimeState;
};
const NotificationContext = createContext<Center | null>(null);
function useCenter() {
  const value = useContext(NotificationContext);
  if (!value) throw new Error("NotificationProvider is required.");
  return value;
}

// Mount one provider per authenticated layout. Scope/identity keys prevent cache sharing between accounts.
export function NotificationProvider({ scope, children }: { scope: NotificationScope; children: ReactNode }) {
  const base = "/api/" + scope + "/notifications";
  const [summary, setSummary] = useState<NotificationSummary | null>(null);
  const [failure, setFailure] = useState<NotificationError | null>(null);
  const [revision, setRevision] = useState(0), [busy, setBusy] = useState(false);
  const previous = useRef<NotificationSummary | null>(null), sequence = useRef(0);
  const active = useRef(false), currentRequest = useRef<AbortController | null>(null);
  const channel = useRef<BroadcastChannel | null>(null);
  const live = useRef(false);
  const [realtimeState,setRealtimeState] = useState<RealtimeState>("connecting");
  const refreshSummary = useCallback(async () => {
    const version = ++sequence.current;
    currentRequest.current?.abort(); const controller = new AbortController(); currentRequest.current = controller;
    try {
      const next = await request<NotificationSummary>(base + "/unread-count", "GET", controller.signal);
      if (!Number.isSafeInteger(next.unreadCount) || next.unreadCount < 0 || !Number.isSafeInteger(next.latestSequence) ||
        (next.unseenOrderCount !== undefined && (!Number.isSafeInteger(next.unseenOrderCount) || next.unseenOrderCount < 0))) throw new NotificationError(502, "");
      if (!active.current || version !== sequence.current) return true;
      const before = previous.current;
      previous.current = next; setSummary(next); setFailure(null);
      if (before && (before.unreadCount !== next.unreadCount || before.latestSequence !== next.latestSequence || before.unreadSupportCount !== next.unreadSupportCount)) setRevision(value => value + 1);
      return true;
    } catch (cause) {
      if (!active.current || controller.signal.aborted || version !== sequence.current) return true;
      const error = cause instanceof NotificationError ? cause : new NotificationError(0, "");
      setFailure(error);
      if ([401, 403].includes(error.status)) { previous.current = null; setSummary(null); setRevision(value => value + 1); }
      // Stop periodic failures for expired sessions or a pending migration; focus/manual retry can recover.
      return ![401, 403].includes(error.status) && error.code !== "NOTIFICATIONS_UNAVAILABLE";
    }
  }, [base]);
  const refresh = useCallback(() => {
    // Route manual refresh through the serial poller so it also resumes after unavailable storage.
    window.dispatchEvent(new Event("chap:notifications-refresh"));
  }, []);
  useEffect(() => {
    active.current = true;
    const stop = startNotificationPolling(refreshSummary, {
      visible: () => document.visibilityState === "visible",
      schedule: (callback, delay) => window.setTimeout(callback, delay), cancel: timer => window.clearTimeout(timer as number),
      interval: () => live.current ? 120_000 : 20_000,
      listen: (event, callback) => {
        const target = event === "visibilitychange" ? document : window;
        const name = event === "mutation" ? "chap:notifications-refresh" : event;
        target.addEventListener(name, callback); return () => target.removeEventListener(name, callback);
      },
    });
    const disconnect = connectRealtime(scope, "notifications", refresh, state => {
      const wasLive = live.current; live.current = state === "connected";
      if (active.current) setRealtimeState(state);
      if (wasLive && !live.current) refresh();
    });
    const broadcast = typeof BroadcastChannel === "undefined" ? null : new BroadcastChannel("chap-giti-notifications-" + scope);
    channel.current = broadcast;
    if (broadcast) broadcast.onmessage = () => {
      if (document.visibilityState !== "visible") return; // Focus will fetch the persisted state later.
      refresh();
    };
    return () => { active.current = false; live.current = false; disconnect(); stop(); currentRequest.current?.abort(); broadcast?.close(); channel.current = null; };
  }, [scope, refreshSummary, refresh]);
  const read = useCallback(async (id?: string) => {
    setBusy(true);
    try {
      await request(base + (id ? "/" + encodeURIComponent(id) + "/read" : "/read-all"), "POST");
      refresh(); channel.current?.postMessage("changed");
    } catch (cause) {
      if (cause instanceof NotificationError && [401,403].includes(cause.status)) {
        previous.current = null; setSummary(null); setFailure(cause); setRevision(value => value + 1);
      }
      throw cause;
    } finally { setBusy(false); }
  }, [base, refresh]);
  return <NotificationContext.Provider value={{ scope, base, summary, error: failure?.message || "", expired: !!failure && [401,403].includes(failure.status), revision, refresh, read, busy, realtimeState }}>{children}</NotificationContext.Provider>;
}

function NotificationList({ compact = false }: { compact?: boolean }) {
  const center = useCenter();
  const [page, setPage] = useState(1), [retry, setRetry] = useState(0), [actionError, setActionError] = useState("");
  const key = [center.base, compact ? 1 : page, compact ? 5 : 20, center.revision, retry].join(":");
  const [resource, setResource] = useState<{ key: string; data: NotificationPage | null; error: string } | null>(null);
  useEffect(() => {
    if (center.expired) return;
    const controller = new AbortController();
    request<NotificationPage>(center.base + "?page=" + (compact ? 1 : page) + "&pageSize=" + (compact ? 5 : 20), "GET", controller.signal)
      .then(data => { if (!controller.signal.aborted) setResource({ key, data, error: "" }); })
      .catch(cause => { if (!controller.signal.aborted) setResource({ key, data: null, error: cause instanceof NotificationError ? cause.message : "ارتباط با مرکز اعلان برقرار نشد. دوباره تلاش کنید." }); });
    return () => controller.abort();
  }, [key, center.base, center.expired, compact, page]);
  const current = resource?.key === key;
  const data = current && !center.expired ? resource.data : null;
  const error = center.expired ? center.error : current ? resource.error : "";
  const retryList = () => { setRetry(value => value + 1); center.refresh(); };
  async function read(id?: string) {
    setActionError("");
    try {
      await center.read(id);
    } catch (cause) { setActionError(cause instanceof NotificationError ? cause.message : "خوانده‌شدن اعلان ذخیره نشد. دوباره تلاش کنید."); }
  }
  return <div className={"notification-list " + (compact ? "compact" : "")}>
    <div className="notification-tools">
      <button type="button" className="text-link" onClick={() => { void read(); }} disabled={center.busy || center.expired || !center.summary?.unreadCount}><CheckCheck size={16} aria-hidden="true" />خواندن همه</button></div>
    {actionError && <p role="alert" className="error-message">{actionError}</p>}
    {center.error && !error && <p className="notification-warning" role="status">{center.error}</p>}
    {!current && !center.expired && <p role="status" className="notification-state">در حال دریافت اعلان‌ها…</p>}
    {error && <div className="notification-state error-message" role="alert"><p>{error}</p>{center.expired ? <Link prefetch={false} href={center.scope === "admin" ? "/admin/login?next=/admin/notifications" : "/customer?next=/account/notifications"}>ورود دوباره</Link> : <button type="button" className="text-link" onClick={retryList}>تلاش دوباره</button>}</div>}
    {data && <>{data.items.length ? <ul className="notification-items">{uniqueById(data.items).map((item: NotificationItem) => <li key={item.id} data-notification-id={item.id} className={"notification-item " + (item.readAt ? "is-read" : "is-unread")}>
      <div className="notification-open">
        <strong>{item.title}</strong><span>{item.body}</span>{item.orderCode && <span>کد سفارش <bdi>{item.orderCode}</bdi></span>}<span className="notification-meta"><time dateTime={item.createdAt}>{notificationTime(item.createdAt)}</time><span>{item.readAt ? "خوانده‌شده" : "خوانده‌نشده"}</span></span>
      </div>{!item.readAt && <button type="button" className="notification-read icon-button" aria-label={"خواندن اعلان: " + item.title} title="علامت‌گذاری به‌عنوان خوانده‌شده" disabled={center.busy} onClick={() => { void read(item.id); }}><Check size={18} aria-hidden="true" /></button>}
    </li>)}</ul> : <p className="notification-state">هنوز اعلانی ندارید.</p>}
      {!compact && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="اعلان" onPageChange={setPage} />}</>}
  </div>;
}

export function NotificationBell() {
  const center = useCenter(), titleId = useId(), [open, setOpen] = useState(false);
  const count = center.summary?.unreadCount || 0;
  const title = "اعلان‌ها";
  return <Popover open={open} onOpenChange={setOpen}><PopoverTrigger asChild><button type="button" className="notification-bell icon-button" aria-label={title + (count ? "، " + count.toLocaleString("fa-IR") + " خوانده‌نشده" : "")}>
    <Bell size={21} aria-hidden="true" />{count > 0 && <bdi className="notification-badge" aria-hidden="true">{count > 99 ? "99+" : count.toLocaleString("fa-IR")}</bdi>}
    <span className="sr-only" role="status" aria-live="polite" aria-atomic="true">{center.error || (center.summary ? count.toLocaleString("fa-IR") + " اعلان خوانده‌نشده" : "در حال دریافت شمار اعلان‌ها")}</span>
  </button></PopoverTrigger><PopoverContent className="notification-popover" align="end" dir="rtl" aria-labelledby={titleId} collisionPadding={12}>
    <div className="notification-heading"><h2 id={titleId}>{title}</h2><Link prefetch={false} href={center.scope === "admin" ? "/admin/notifications" : "/account/notifications"} onClick={() => setOpen(false)}>مشاهده همه</Link></div>
    <NotificationList compact />
  </PopoverContent></Popover>;
}

export function NotificationCenter() {
  const center = useCenter();
  return <section className="panel notification-center" aria-label="فهرست اعلان‌ها" data-realtime-state={center.realtimeState}><NotificationList /></section>;
}

export function NavigationBadge({kind,label}:{kind:"orders"|"notifications"|"support";label:string}) {
  const center=useContext(NotificationContext);
  const count=(kind==="orders" ? center?.summary?.unseenOrderCount : kind==="support" ? center?.summary?.unreadSupportCount : center?.summary?.unreadCount) || 0;
  if(!count)return null;
  return <span className="navigation-badge" data-badge={kind} title={count.toLocaleString("fa-IR")+" "+label}>
    <bdi aria-hidden="true">{count>99?"99+":count.toLocaleString("fa-IR")}</bdi><span className="sr-only">{count.toLocaleString("fa-IR")} {label}</span>
  </span>;
}

// Render this only with authorized detail data. Opening a menu/list never calls it.
export function OrderSeen({orderId,sequence,available}:{orderId:string;sequence?:number;available?:boolean}) {
  const center=useContext(NotificationContext),anchor=useRef<HTMLDivElement>(null);
  const [retry,setRetry]=useState(0),[error,setError]=useState("");
  const acknowledged=useRef("");
  const refresh=center?.refresh,scope=center?.scope;
  useEffect(()=>{
    if(!available||!scope||sequence===undefined||!anchor.current)return;
    const key=orderId+":"+sequence;if(acknowledged.current===key)return;
    let intersects=false,active=true,inflight=false;
    const controller=new AbortController();
    async function seen(){
      if(!active||!intersects||document.visibilityState!=="visible"||inflight||acknowledged.current===key)return;
      inflight=true;
      try {
        const response=await sessionFetch("/api/"+scope+"/orders/"+orderId+"/view",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({seenThroughSequence:sequence}),signal:controller.signal});
        if(!response.ok)throw new Error("مشاهدهٔ سفارش ذخیره نشد.");
        if(active){acknowledged.current=key;setError("");refresh?.();}
      }catch(cause){if(active&&!controller.signal.aborted)setError(cause instanceof Error?cause.message:"مشاهدهٔ سفارش ذخیره نشد.");}
      finally{inflight=false;}
    }
    // Observe the actual detail region, including restored scroll positions;
    // a tiny top sentinel would miss customers reading the lower history.
    const region=anchor.current.closest(".customer-order-details,.order-editor")||anchor.current;
    const observer=new IntersectionObserver(entries=>{intersects=entries.some(e=>e.isIntersecting);void seen();},{threshold:0});
    observer.observe(region);
    const onVisible=()=>void seen();document.addEventListener("visibilitychange",onVisible);window.addEventListener("focus",onVisible);
    return()=>{active=false;controller.abort();observer.disconnect();document.removeEventListener("visibilitychange",onVisible);window.removeEventListener("focus",onVisible);};
  },[available,orderId,sequence,scope,refresh,retry]);
  return <div ref={anchor} className="order-seen-anchor">{error&&<p role="status">{error} <button className="text-link" onClick={()=>setRetry(n=>n+1)}>تلاش دوباره</button></p>}</div>;
}
