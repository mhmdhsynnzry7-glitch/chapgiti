"use client";
import Pagination from "./pagination";
import { formatToman } from "@/lib/money";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import {
  LayoutDashboard,
  Images,
  ShoppingBag,
  Users,
  FileText,
  Settings,
  History,
  ExternalLink,
  LogOut,
  Menu,
  X,
  ArrowUpLeft,
  Download,
  Plus,
  Printer,
  Wallet,
  Save,
  Search,
} from "lucide-react";
import ManagementTable, { DateSort, PageSizeControl } from "./management-table";
import AdminTickets from "./admin-tickets";
import { NavigationBadge, NotificationBell } from "./notification-center";
import { orderServiceLabel,orderServices,optionalOrderPdfs } from "@/lib/order-requirements";
import AdminServices from "./admin-services";
import AdminMagazine from "./admin-magazine";
import AdminPrintCatalog from "./admin-print-catalog";
import AdminBanners from "./admin-banners";
import AdminUsers from "./admin-users";
import AdministratorProfile from "./administrator-profile";
import Administrators from "./administrators";
import { sessionFetch } from "@/lib/session-client";
import { ActionFilter, OrderAttention } from "./order-attention";
import AdminOrderDetail, { type AdminOrderDetailData } from "./admin-order-detail";
import CustomerActivity from "./customer-activity";
import CutTariffEditor from "./cut-tariff-editor";
import { finishingServices } from "@/lib/book-options";
import { Brand, Empty, Status } from "./ui";
import { api } from "@/lib/api-client";
import { useApiResource as useData } from "@/hooks/use-api-resource";
import type { OrderSummary, OverviewResult, OrderListResult, ContactMessage, SettingsResult, LogEntry } from "@/lib/api-contracts";
import { money, date, adminStatuses as statuses } from "@/lib/pricing";
const navigation = [
  { id: "overview", title: "نمای کلی", icon: LayoutDashboard },
  { id: "orders", title: "سفارش‌ها", icon: ShoppingBag },
  { id: "customers", title: "مشتریان", icon: Users },
  { id: "profile", title: "پروفایل مدیر", icon: Settings },
  { id: "banners", title: "بنرهای تبلیغات", icon: Images },
  { id: "tickets", title: "پشتیبانی", icon: FileText },
  { id: "services", title: "مدیریت خدمات", icon: FileText },
  { id: "articles", title: "مجله", icon: FileText },
  { id: "print-settings", title: "تنظیمات چاپ", icon: Printer },
  { id: "settings", title: "تنظیمات و تعرفه‌ها", icon: Settings },
  { id: "activity", title: "تاریخچه فعالیت کاربران", icon: History },
  { id: "audit", title: "تاریخچه مدیریت", icon: History },
];
function Loading() {
  return (
    <div className="loading" role="status">
      <span className="spinner" />
      در حال دریافت اطلاعات…
    </div>
  );
}
function ErrorState({ error, reload }: { error: string; reload: () => void }) {
  return (
    <div className="error-message" role="alert">
      {error}
      <button
        className="button button-sm button-outline"
        style={{ marginRight: 18 }}
        onClick={reload}
      >
        تلاش دوباره
      </button>
    </div>
  );
}
type Notify = (message: string, error?: boolean) => void;
export default function AdminApp({
  section,
  user,
  initialOrderId,
  initialOverview,
  initialOrder,
}: {
  section: string;
  user: { id: string; email: string; name: string; legacy?: boolean };
  initialOrderId?: string;
  initialOverview?: OverviewResult;
  initialOrder?: AdminOrderDetailData;
}) {
  const [mobile, setMobile] = useState(false);
  const [collapsed,setCollapsed]=useState(false);
  const [toast, setToast] = useState<{ text: string; error: boolean } | null>(
    null,
  );
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 5000);
    return () => clearTimeout(t);
  }, [toast]);
  const notify: Notify = (text, error = false) => setToast({ text, error });
  const listFilters = useSearchParams().toString();
  const title = navigation.find((n) => n.id === section)?.title || "نمای کلی";
  return (
    <div className={"admin-shell"+(collapsed?" sidebar-collapsed":"")}>
      <aside className={"admin-sidebar " + (mobile ? "open" : "")}>
        <Brand light />
        <button className="sidebar-collapse icon-button" type="button" onClick={()=>setCollapsed(value=>!value)} aria-label={collapsed?"گسترش نوار کناری":"جمع‌کردن نوار کناری"} aria-expanded={!collapsed}><Menu size={19}/></button>
        <button
          className="admin-mobile-toggle icon-button"
          style={{ color: "white", position: "absolute", left: 5, top: 5 }}
          onClick={() => setMobile(false)}
          aria-label="بستن منو"
        >
          <X size={16} />
        </button>
        <span className="sidebar-label">مدیریت چاپخانه</span>
        <nav className="admin-nav">
          {navigation.map((n) => (
            <Link prefetch={false}
              href={"/admin/" + (n.id === "overview" ? "" : n.id) + (n.id === "orders" && section === "orders" && listFilters ? "?" + listFilters : "")}
              key={n.id}
              className={section === n.id ? "active" : ""}
              aria-current={section === n.id ? "page" : undefined}
              title={collapsed?n.title:undefined}
              onClick={()=>setMobile(false)}
            >
              <n.icon />
              <span className="admin-nav-title">{n.title}</span>
              {n.id==="tickets"&&<NavigationBadge kind="support" label="پیام پشتیبانی خوانده‌نشده"/>}
              {n.id==="orders"&&<NavigationBadge kind="orders" label="سفارش دیده‌نشده"/>}
            </Link>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <a href="/" target="_blank">
            <ExternalLink size={15} />
            مشاهده وب‌سایت
          </a>
          <button type="button" className="text-link" onClick={async () => { const r = await sessionFetch("/api/auth/logout", { method: "POST" }); if (r.ok) window.location.assign("/admin/login"); }}>
            <LogOut size={15} />
            خروج از حساب
          </button>
        </div>
      </aside>
      <div className="admin-main">
        <header className="admin-topbar">
          <button
            className="admin-mobile-toggle icon-button"
            onClick={() => setMobile(!mobile)}
            aria-label="منوی مدیریت"
            aria-expanded={mobile}
          >
            <Menu />
          </button>
          <div>
            <h1>{title}</h1>
            <small>پنل مدیریت چاپ گیتی</small>
          </div>
          <div className="admin-profile">
            <NotificationBell />
            <div>
              <strong>{user.name}</strong>
              <small>مدیر چاپخانه</small>
            </div>
            <span className="avatar">م</span>
          </div>
        </header>
        <main className="admin-content" id="main">
          {section === "overview" ? (
            <Overview initialData={initialOverview} />
          ) : section === "orders" ? (
            <Orders key={initialOrderId || "list"} notify={notify} initialId={initialOrderId} initialDetail={initialOrder} />
          ) : section === "customers" ? (
            <AdminUsers userId={user.id} notify={notify} />
          ) : section === "profile" ? (
            <AdministratorProfile email={user.email} />
          ) : section === "administrators" ? (
            <Administrators />
          ) : section === "users" ? (
            <AdminUsers userId={user.id} notify={notify} />
          ) : section === "banners" ? (
            <AdminBanners notify={notify} />
          ) : section === "services" ? (
            <AdminServices initialId={initialOrderId} notify={notify} />
          ) : section === "articles" ? (
            <AdminMagazine initialId={initialOrderId} notify={notify} />
          ) : section === "print-settings" ? (
            <AdminPrintCatalog initialId={initialOrderId} notify={notify} />
          ) : section === "tickets" ? (
            <AdminTickets initialId={initialOrderId} />

          ) : section === "messages" ? (
            <Messages notify={notify} />
          ) : section === "settings" ? (
            <SettingsPage notify={notify} />
          ) : section === "sms" ? (
            <Logs type="sms" />
          ) : section === "activity" ? (
            <CustomerActivity />
          ) : section === "audit" ? (
            <Logs type="audit" />
          ) : (
            <Empty title="بخش مورد نظر پیدا نشد" />
          )}
        </main>
      </div>
      {toast && (
        <div role="status" className={"toast " + (toast.error ? "error" : "")}>
          {toast.text}
        </div>
      )}
    </div>
  );
}
function OrderTable({
  orders,
  onOpen,
}: {
  orders: OrderSummary[];
  onOpen?: (id: string) => void;
}) {
  return <ManagementTable remote title="سفارش‌ها" columns={["سفارش / مشتری","آخرین تغییر","مبلغ","وضعیت"]} rows={orders.map(o=>({id:o.id,emphasis:o.attention?.required || o.attention?.unread,values:[o.title,date(o.updated_at || o.created_at),formatToman(o.final_price??o.estimate),statuses[o.status]],
    cells:[<span key={o.id}>{o.title}<small>{orderServiceLabel(o.service)}</small><OrderAttention state={o.attention}/><small>{o.customer?.trim()||o.phone} · <bdi>{o.code}</bdi></small></span>,null,null,<Status key={o.status} value={o.status} label={statuses[o.status]}/>],
    actions:onOpen?<button onClick={()=>onOpen(o.id)}>{o.attention?.required ? o.attention.label : "بررسی سفارش"}</button>:<Link href={"/admin/orders/"+o.id}>مشاهده</Link>}))}/>;
}
function Overview({ initialData }: { initialData?: OverviewResult }) {
  const { data, error, loading, reload } = useData<OverviewResult>("admin/overview", true, initialData);
  if (error) return <ErrorState error={error} reload={reload} />;
  if (loading || !data) return <Loading />;
  const total = Number(data.summary.total);
  const active = data.counts
    .filter((x) => !["completed", "cancelled"].includes(x.status))
    .reduce((n, x) => n + x.count, 0);
  const max = Math.max(1, ...data.trend.map((x) => x.count));
  return (
    <>
      <div className="admin-heading">
        <div>
          <span className="eyebrow">یک نگاه به جریان کار</span>
          <h2>روز تازه، داستان‌های تازه.</h2>
          <p>وضعیت چاپخانه و سفارش‌ها را اینجا دنبال کنید.</p>
        </div>
        <a href="/order" className="button button-sm" target="_blank">
          <Plus size={16} />
          سفارش جدید
        </a>
      </div>
      <div className="metrics-grid">
        {[
          {
            t: "کل سفارش‌ها",
            v: total,
            s: "تمام سفارش‌های ثبت‌شده",
            i: ShoppingBag,
          },
          {
            t: "در جریان تولید",
            v: active,
            s: "از بررسی تا آماده تحویل",
            i: Printer,
          },
          {
            t: "مشتریان",
            v: data.summary.customers,
            s: "بر اساس شماره همراه یکتا",
            i: Users,
          },
          {
            t: "ارزش سفارش‌ها",
            v: Number(data.summary.value),
            s: "شامل برآوردها، بدون لغوشده",
            i: Wallet,
          },
        ].map((m) => (
          <div className="metric" key={m.t}>
            <div className="metric-top">
              <span>{m.t}</span>
              <m.i />
            </div>
            <strong>{m.i === Wallet ? formatToman(m.v) : money(m.v)}</strong>
            <small>{m.s}</small>
          </div>
        ))}
      </div>
      <div className="admin-grid">
        <section className="panel">
          <div className="panel-title">
            <h3>روند ثبت سفارش</h3>
            <span>۳۰ روز اخیر</span>
          </div>
          {data.trend.length ? (
            <div
              className="chart-area"
              role="img"
              aria-label={
                "تعداد سفارش روزانه: " +
                data.trend.map((d) => d.day + ": " + d.count).join("، ")
              }
            >
              {data.trend.map((d) => (
                <div
                  className="chart-col"
                  key={d.day}
                  style={{ height: Math.max(6, (d.count / max) * 145) }}
                >
                  <b>{money(d.count)}</b>
                  <span>
                    {new Intl.DateTimeFormat("fa-IR", {
                      month: "numeric",
                      day: "numeric",
                    }).format(new Date(d.day))}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <Empty
              title="هنوز سفارشی ثبت نشده"
              text="نمودار با ثبت اولین سفارش شکل می‌گیرد."
            />
          )}
        </section>
        <section className="panel">
          <div className="panel-title">
            <h3>وضعیت سفارش‌ها</h3>
            <span>{money(total)} سفارش</span>
          </div>
          <div className="status-progress">
            {["new", "review", "awaiting_customer_approval", "confirmed", "printing", "binding", "ready", "completed", "cancelled"].map(
              (s) => {
                const count =
                  data.counts.find((c) => c.status === s)?.count || 0;
                return (
                  <div key={s}>
                    <div className="progress-label">
                      <span>{statuses[s]}</span>
                      <span>{money(count)}</span>
                    </div>
                    <div className="progress-track">
                      <div
                        style={{
                          width: (total ? (count / total) * 100 : 0) + "%",
                        }}
                      />
                    </div>
                  </div>
                );
              },
            )}
          </div>
        </section>
      </div>
      <section className="panel table-panel">
        <div className="panel-title">
          <h3>جدیدترین سفارش‌ها</h3>
          <Link prefetch={false} className="text-link" href="/admin/orders">
            همه سفارش‌ها <ArrowUpLeft size={16} />
          </Link>
        </div>
        {data.recent.length ? (
          <OrderTable orders={data.recent} />
        ) : (
          <Empty
            title="اولین سفارش در راه است"
            text="سفارش‌های وب‌سایت به صورت خودکار اینجا نمایش داده می‌شوند."
          />
        )}
      </section>
      <div className="notice">
      </div>
    </>
  );
}
function Orders({ notify, initialId, initialDetail }: { notify: Notify; initialId?: string; initialDetail?: AdminOrderDetailData }) {
  const router = useRouter();
  const params = useSearchParams();
  const selected = initialId;
  const query = params.get("q") ?? "";
  const [search, setSearch] = useState(query);
  const status = params.get("status") ?? "";
  const attention = params.get("attention") ?? "";
  const oldestFirst = params.get("oldestFirst") === "true";
  const pageSize = Number(params.get("pageSize")) || 20;
  const page = Math.max(1, Number(params.get("page")) || 1);
  const suffix = params.size ? "?" + params.toString() : "";
  function filter(values: Record<string, string | number | boolean>) {
    const next = new URLSearchParams(params.toString());
    for (const [key,value] of Object.entries<string | number | boolean>({page:1, ...values})) {
      if (value === "" || value === false || (key === "page" && value === 1)) next.delete(key);
      else next.set(key, String(value));
    }
    router.replace("/admin/orders" + (next.size ? "?" + next : ""), {scroll:false});
  }
  const { data, error, loading, reload } = useData<OrderListResult>(
    "admin/orders?q=" +
      encodeURIComponent(query) +
      "&status=" +
      status +
      "&page=" +
      page+"&pageSize="+pageSize+"&oldestFirst="+oldestFirst+"&attention="+attention,
    true,
    undefined,
    !selected,
  );
  if (selected)
    return (
      <AdminOrderDetail
        key={selected}
        id={selected}
        initialData={selected === initialId ? initialDetail : undefined}
        notify={notify}
        back={() => router.push("/admin/orders" + suffix)}
      />
    );
  return (
    <>
      <div className="admin-heading">
        <div>
          <h2>مدیریت سفارش‌ها</h2>
          <p>جست‌وجو، بررسی فایل و پیگیری روند تولید</p>
        </div>
        <a className="button button-outline button-sm" href="/api/admin/export" download="orders.csv">
          <Download size={16} />
          خروجی CSV
        </a>
      </div>
      <section className="panel table-panel">
        <form
          className="filterbar"
          onSubmit={(e) => {
            e.preventDefault();
            filter({q:search.trim()});
          }}
        >
          
          <DateSort oldest={oldestFirst} change={v=>filter({oldestFirst:v})}/>
          <button type="button" className="icon-button" disabled={loading} onClick={reload} aria-label="تازه‌سازی سفارش‌ها" title="تازه‌سازی سفارش‌ها">↻</button>
          <input
            className="search-input"
            aria-label="جست‌وجوی سفارش"
            placeholder="عنوان، مشتری، شماره یا کد سفارش…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <ActionFilter value={attention} change={v=>filter({attention:v})}/>
          <select
            aria-label="فیلتر وضعیت"
            value={status}
            onChange={(e) => {
              filter({status:e.target.value});
            }}
          >
            <option value="">همه وضعیت‌ها</option>
            {Object.entries(statuses).map(([s, t]) => (
              <option value={s} key={s}>
                {t}
              </option>
            ))}
          </select>
          <button className="button button-sm button-dark">
            <Search size={17} />
            جست‌وجو
          </button>
        </form>
        {loading ? (
          <Loading />
        ) : error ? (
          <ErrorState error={error} reload={reload} />
        ) : data && data.orders.length ? (
          <OrderTable orders={data.orders} onOpen={id=>router.push("/admin/orders/" + encodeURIComponent(id) + suffix)} />
        ) : (
          <Empty
            title="سفارشی پیدا نشد"
            text="فیلتر را تغییر دهید یا یک سفارش از سایت ثبت کنید."
          />
        )}
        {data && (<>
          <PageSizeControl value={pageSize} change={n=>filter({pageSize:n})}/>
          <Pagination className="pagination" page={page} pageSize={pageSize} total={data.total} noun="سفارش" loading={loading} onPageChange={n=>filter({page:n})} />
        </>)}
      </section>
    </>
  );
}

function Messages({ notify }: { notify: Notify }) {
  const { data, error, loading, reload } = useData<ContactMessage[]>("admin/messages");
  if (error) return <ErrorState error={error} reload={reload} />;
  if (loading || !data) return <Loading />;
  return (
    <>
      <div className="admin-heading">
        <div>
          <h2>صدای مشتریان</h2>
          <p>آخرین ۲۰۰ پیام مشاوره و پشتیبانی</p>
        </div>
      </div>
      {data.length ? (
        data.map((m) => (
          <article
            className={"admin-message " + (m.read ? "read" : "")}
            key={m.id}
          >
            <div>
              <h3>{m.subject}</h3>
              <small>{date(m.created_at)}</small>
            </div>
            <p>{m.message}</p>
            <div>
              <small>
                {m.name} · <Link prefetch={false} href={"tel:" + m.phone}>{m.phone}</Link>
              </small>
              {!m.read && (
                <button
                  className="button button-sm button-outline"
                  onClick={async () => {
                    try {
                      await api("admin/messages/" + m.id, {
                        method: "PATCH",
                        body: "{}",
                      });
                      notify("پیام خوانده‌شده علامت خورد.");
                      reload();
                    } catch (e) {
                      notify((e as Error).message, true);
                    }
                  }}
                >
                  خوانده شد
                </button>
              )}
            </div>
          </article>
        ))
      ) : (
        <section className="panel">
          <Empty title="پیام تازه‌ای ندارید" />
        </section>
      )}
    </>
  );
}
function SettingsPage({ notify }: { notify: Notify }) {
  const { data, error, loading, reload } = useData<SettingsResult>("admin/settings");
  if (error) return <ErrorState error={error} reload={reload} />;
  if (loading || !data) return <Loading />;
  return <SettingsEditor initial={data} notify={notify} />;
}
function SettingsEditor({ initial, notify }: { initial: SettingsResult; notify: Notify }) {
  const [site, setSite] = useState(initial.site);
  const [rates, setRates] = useState<Partial<typeof initial.rates>>(initial.rates);
  const [orderRequirements,setOrderRequirements]=useState(initial.orderRequirements||optionalOrderPdfs);
  const [tab, setTab] = useState("site");
  const [cutDirty, setCutDirty] = useState(false);
  const [savedRates, setSavedRates] = useState(rates);
  const finishingDirty = Object.keys(finishingServices).some(key => rates[key as keyof typeof rates] !== savedRates[key as keyof typeof rates]);
  const unsaved = tab === "rates" && (cutDirty || finishingDirty);
  useEffect(() => {
    if (!unsaved) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    const navigate = (event: MouseEvent) => {
      const link = event.target instanceof Element ? event.target.closest<HTMLAnchorElement>("a[href]") : null;
      if (!link || link.target === "_blank" || event.ctrlKey || event.metaKey || event.shiftKey || link.href === location.href) return;
      if (!window.confirm("تغییرات تعرفه ذخیره نشده است. از صفحه خارج می‌شوید؟")) {
        event.preventDefault(); event.stopPropagation();
      }
    };
    window.addEventListener("beforeunload", warn); document.addEventListener("click", navigate, true);
    return () => {window.removeEventListener("beforeunload", warn); document.removeEventListener("click", navigate, true);};
  }, [unsaved]);
  const [busy, setBusy] = useState(false);
  async function save(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api("admin/settings", {
        method: "PUT",
        body: JSON.stringify(tab === "rates" ? { finishingRates: Object.fromEntries(Object.keys(finishingServices).map(key=>[key,rates[key as keyof typeof rates]])) } : { site, orderRequirements }),
      });
      if (tab === "rates") setSavedRates({...rates});
      notify("تنظیمات ذخیره شد.");
    } catch (e) {
      notify((e as Error).message, true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="editor-wrap">
      <div className="admin-heading">
        <div>
          <h2>تنظیمات چاپخانه</h2>
          <p>هویت سایت، اطلاعات تماس و مبنای محاسبه قیمت</p>
        </div>
      </div>
      <div className="admin-tabs">
        {[
          ["site", "اطلاعات سایت"],
          ["rates", "تعرفه‌های چاپ"],
          ["seo", "تنظیمات سئو"],
        ].map(([k, t]) => (
          <button
            className={tab === k ? "active" : ""}
            aria-pressed={tab === k}
            onClick={() => {
              if (k === tab || (unsaved && !window.confirm("تغییرات تعرفه ذخیره نشده است. بخش دیگری باز شود؟"))) return;
              if (tab === "rates") {setCutDirty(false); setRates(savedRates);}
              setTab(k);
            }}
            key={k}
          >
            {t}
          </button>
        ))}
      </div>
      {tab === "rates" && <CutTariffEditor onDirty={setCutDirty}/>}
      <form onSubmit={save} className="panel">
        {tab === "site" ? (
          <div className="form-grid">
            {[
              ["brand", "نام چاپخانه / انتشارات"],
              ["tagline", "شعار کوتاه"],
              ["phone", "شماره تماس"],
              ["email", "ایمیل عمومی"],
              ["address", "نشانی"],
              ["hours", "ساعات پاسخ‌گویی"],
            ].map(([k, t]) => (
              <label
                className={"field " + (k === "address" ? "full" : "")}
                key={k}
              >
                {t}
                <input
                  value={site[k as keyof typeof site]}
                  required={k === "brand"}
                  type={k === "email" ? "email" : "text"}
                  maxLength={k === "address" ? 300 : 120}
                  onChange={(e) => setSite({ ...site, [k]: e.target.value })}
                />
              </label>
            ))}
            {[ ["instagram", "اینستاگرام"], ["telegram", "تلگرام"], ["bale", "بله"] ].map(([key, title]) => <label className="field" key={key}><span id={"social-label-"+key}>لینک {title}</span><input type="url" dir="ltr" maxLength={500} aria-labelledby={"social-label-"+key} aria-describedby={"social-hint-"+key} value={site[key as "instagram" | "telegram" | "bale"] || ""} placeholder="https://…" onChange={event => setSite({ ...site, [key]: event.target.value })} /><small id={"social-hint-"+key}>اختیاری؛ پیوند حساب یا کانال، بدون پارامتر اضافی.</small></label>)}
            <fieldset className="full order-document-settings"><legend>مدارک لازم هنگام ثبت سفارش</legend><p>الزام PDF برای هر خدمت را مشخص کنید. این تنظیم فقط روی سفارش‌های جدید اثر دارد.</p>{Object.entries(orderServices).map(([id,label])=><label className="check-field" key={id}><input type="checkbox" checked={orderRequirements[id]===true} onChange={e=>setOrderRequirements({...orderRequirements,[id]:e.target.checked})}/>PDF اولیه برای {label} الزامی است</label>)}</fieldset>
          </div>
        ) : tab === "rates" ? (
          <>
            <h3 className="tariff-section-title">تعرفه خدمات تکمیلی</h3>
            <p className="settings-note">مبلغ هر خدمت به تومان برای هر جلد است. همین نرخ در تمام قطع‌ها اعمال می‌شود.</p>
            <div className="form-grid">{Object.entries(finishingServices).map(([key, service]) => {
              const tariff = key as keyof typeof rates;
              return <label className="field" key={key}>{service.label}
                <input aria-label={"تعرفه " + service.label} type="number" required min={0} max={10000000} step={1} dir="ltr" value={Number.isFinite(rates[tariff]) ? rates[tariff] : ""} onChange={e => setRates({ ...rates, [tariff]: e.target.value === "" ? NaN : Number(e.target.value) })} />
                <small>{formatToman(rates[tariff])} · هر جلد</small>
                {service.hard && <small>مخصوص جلد سخت</small>}{service.matte && <small>نیازمند سلفون مات</small>}
              </label>;
            })}</div>
          </>
        ) : tab === "seo" ? (
          <>
            <div className="form-grid">
              <label className="field full">
                عنوان صفحه اصلی
                <input
                  required
                  minLength={5}
                  maxLength={100}
                  value={site.seoTitle}
                  onChange={(e) =>
                    setSite({ ...site, seoTitle: e.target.value })
                  }
                />
              </label>
              <label className="field full">
                توضیحات صفحه اصلی
                <textarea
                  required
                  minLength={20}
                  maxLength={180}
                  value={site.seoDescription}
                  onChange={(e) =>
                    setSite({ ...site, seoDescription: e.target.value })
                  }
                />
                <small>{money(site.seoDescription.length)} از ۱۸۰ نویسه</small>
              </label>
            </div>
            <div className="meta-preview">
              <h3>{site.seoTitle}</h3>
              <p>{site.seoDescription}</p>
            </div>
            <p className="notice">
              نقشه سایت، لینک canonical و داده ساختاریافته خودکار تولید می‌شوند.
              ایندکس شدن نسخه خصوصی غیرفعال است؛ روی دامنه عمومی نهایی، تنظیم
              INDEXING_ENABLED باید فعال شود.
            </p>
          </>
        ) : null}
        <button className="button" disabled={busy} style={{ marginTop: 25 }}>
          <Save size={17} />{busy ? "در حال ذخیره…" : tab === "rates" ? "ذخیره خدمات تکمیلی" : "ذخیره تنظیمات"}
        </button>
        {tab === "rates" && finishingDirty && <button type="button" disabled={busy} className="button button-outline" onClick={() => setRates(savedRates)}>لغو تغییرات خدمات تکمیلی</button>}
      </form>
    </div>
  );
}
function Logs({ type }: { type: "sms" | "audit" }) {
  const { data, error, loading, reload } = useData<LogEntry[]>("admin/" + type);
  if (error) return <ErrorState error={error} reload={reload} />;
  if (loading || !data) return <Loading />;
  const names: Record<string, string> = {
    disabled: "غیرفعال",
    accepted: "پذیرفته‌شده",
    failed: "ناموفق",
    sending: "در حال ارسال",
  };
  return (
    <>
      <div className="admin-heading">
        <div>
          <h2>
            {type === "sms" ? "گزارش پیامک‌های سفارش" : "تاریخچه فعالیت مدیر"}
          </h2>
          <p>
            {type === "sms"
              ? "آخرین ۲۰۰ پیام · پذیرش توسط سرویس به معنی تحویل قطعی نیست."
              : "آخرین ۱۰۰ تغییر مدیریتی"}
          </p>
        </div>
      </div>
      <section className="panel table-panel">
        {!data.length ? (
          <Empty title="گزارشی ثبت نشده است" />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>تاریخ</th>
                  <th>{type === "sms" ? "شماره همراه" : "مدیر"}</th>
                  <th>{type === "sms" ? "وضعیت" : "فعالیت"}</th>
                  {type === "sms" && (
                    <>
                      <th>رسید</th>
                      <th>خطا</th>
                    </>
                  )}
                </tr>
              </thead>
              <tbody>
                {data.map((l) => (
                  <tr key={l.id}>
                    <td>{date(l.created_at)}</td>
                    <td dir="ltr">{type === "sms" ? l.phone : l.actor}</td>
                    <td>
                      {type === "sms" ? (
                        <Status
                          value={l.status || ""}
                          label={names[l.status || ""] || l.status}
                        />
                      ) : (
                        l.action
                      )}
                    </td>
                    {type === "sms" && (
                      <>
                        <td>{l.receipt || "—"}</td>
                        <td>{l.error || "—"}</td>
                      </>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}
