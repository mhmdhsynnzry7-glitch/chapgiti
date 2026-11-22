"use client";
import ManagementTable, {PageSizeControl} from "./management-table";
import { ActionFilter, OrderAttention } from "./order-attention";
import Pagination from "./pagination";
import { formatToman } from "@/lib/money";
import Link from "next/link";
import { useEffect, useState } from "react";
import { ClipboardList } from "lucide-react";
import type { CustomerOrderPage } from "@/lib/api-contracts";
import { sessionFetch } from "@/lib/session-client";
import { redirectToCustomerLogin } from "@/lib/customer-client";
import { date, statuses } from "@/lib/pricing";
import { Status } from "./ui";

export default function CustomerOrders({ showTitle = true }: { showTitle?: boolean }) {
  const [page, setPage] = useState(1), [retry, setRetry] = useState(0);
  const [pageSize,setPageSize]=useState(10);
  const [group, setGroup] = useState(""), [attention, setAttention] = useState("");
  const [data, setData] = useState<CustomerOrderPage | null>(null);
  const [loading, setLoading] = useState(true), [error, setError] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    async function load() {
      setLoading(true); setError("");
      try {
        const response = await sessionFetch(`/api/customer/orders?page=${page}&pageSize=${pageSize}&group=${group}&attention=${attention}`, { signal: controller.signal });
        if (response.status === 401) { redirectToCustomerLogin(); return; }
        const result = await response.json();
        if (!response.ok) throw new Error(result.error || "دریافت سفارش‌ها انجام نشد.");
        if (active) setData(result);
      } catch (cause) {
        if (active) setError(cause instanceof Error && cause.name !== "TypeError" ? cause.message : "ارتباط برقرار نشد. دوباره تلاش کنید.");
      } finally { if (active) setLoading(false); }
    }
    void load();
    return () => { active = false; controller.abort(); };
  }, [page, pageSize, retry, group, attention]);
  return <section className="panel customer-orders" aria-label="سفارش‌های من" aria-busy={loading}>
    {showTitle && <div className="panel-title"><h2>سفارش‌های من</h2><ClipboardList size={21} aria-hidden="true" /></div>}
    
    <div className="inbox-filters"><ActionFilter value={attention} change={v=>{setAttention(v);setPage(1);}}/><label className="field order-group-filter">مرحلهٔ درخواست<select aria-label="مرحلهٔ درخواست" value={group} onChange={event=>{setGroup(event.target.value);setPage(1);}}>{[["","همه درخواست‌ها"],["review","در انتظار بررسی"],["decision","منتظر تأیید من"],["queued","قیمت تأییدشده"],["production","در حال تولید"],["ready","آماده تحویل"],["completed","تحویل‌شده"],["cancelled","رد / لغو شده"]].map(([key,label])=><option value={key} key={key}>{label}</option>)}</select></label></div>
    {loading ? <p className="order-list-message" role="status">در حال دریافت سفارش‌ها…</p>
      : error ? <div className="order-list-message"><p className="error-message" role="alert">{error}</p><button type="button" className="button button-outline" onClick={() => setRetry(value => value + 1)}>تلاش دوباره</button></div>
      : data && data.items.length ? <>
        <ManagementTable remote title="فهرست سفارش‌ها" refresh={()=>setRetry(n=>n+1)} columns={["عنوان","کد پیگیری","آخرین تغییر","برآورد","قیمت پذیرفته‌شده","وضعیت"]} rows={data.items.map(order=>({id:order.id,emphasis:order.attention?.required || order.attention?.unread,values:[order.title,order.code,date(order.updated_at || order.created_at),formatToman(order.estimate),formatToman(order.final_price),statuses[order.status]],cells:[<span key="title">{order.title}<OrderAttention state={order.attention}/></span>,<bdi key="code">{order.code}</bdi>,null,null,null,<Status key={order.status} value={order.status} label={statuses[order.status]}/>],actions:<Link href={"/account/orders/"+order.id}>{order.attention?.required ? order.attention.label : "مشاهده جزئیات"}</Link>}))}/>

      </> : <div className="order-list-message"><ClipboardList size={38} aria-hidden="true" /><p>{group || attention ? "درخواستی در این گروه وجود ندارد." : "هنوز سفارشی ثبت نکرده‌اید."}</p><Link prefetch={false} className="button" href="/order">ثبت اولین سفارش</Link></div>}
    {!loading && !error && data && <><PageSizeControl value={pageSize} change={n=>{setPageSize(n);setPage(1);}}/><Pagination className="order-pagination" page={page} pageSize={data.pageSize} total={data.total} noun="سفارش" onPageChange={value => { setLoading(true); setPage(value); }} /></>}
  </section>;
}
