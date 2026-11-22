"use client";
import { PageSizeControl } from "./management-table";
import TicketTable from "./ticket-table";
import Pagination from "./pagination";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { Send } from "lucide-react";
import { customerApi } from "@/lib/customer-client";
import { createRequestId } from "@/lib/request-id";
import type { TicketPage } from "@/lib/tickets";
import { useApiResource } from "@/hooks/use-api-resource";
type RelatedOrder = { id: string; title: string; code: string };
export function TicketComposer({ order, consultation, orderLinksAvailable = false }: { order?: RelatedOrder; consultation?: string; orderLinksAvailable?: boolean }) {
  const [busy,setBusy] = useState(false), [error,setError] = useState(""), [requestKey,setRequestKey] = useState("");
  const router=useRouter();
  const [linked,setLinked] = useState(!!order);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy || (linked && !orderLinksAvailable)) return; setBusy(true); setError("");
    const fields = new FormData(event.currentTarget);
    try {
      const idempotencyKey=requestKey || createRequestId(); setRequestKey(idempotencyKey);
      const result=await customerApi("tickets",{method:"POST",body:JSON.stringify({subject:fields.get("subject"),message:fields.get("message"),idempotencyKey,...(consultation ? {consultation:true} : {}),...(linked && order ? { orderId: order.id } : {})})});
      router.push("/tickets/"+result.id);
    } catch (cause) { setError(cause instanceof Error ? cause.message : "ارتباط برقرار نشد."); setBusy(false); }
  }
  return <form id="new-ticket" className="panel ticket-composer" onSubmit={submit} onChange={()=>setRequestKey("")}>
    <div className="panel-title"><h2>تیکت جدید</h2><span>پرسش یا درخواستت را اینجا بنویس.</span></div>
    {consultation && <p className="notice">درخواست مشاوره دربارهٔ {consultation}؛ پاسخ در همین حساب قابل پیگیری است.</p>}
    {order ? <><label className="field">نوع درخواست<select value={linked ? "order" : "general"} onChange={event=>setLinked(event.target.value === "order")}><option value="order">درباره سفارش انتخاب‌شده</option><option value="general">درخواست عمومی</option></select></label>{linked && <p className="notice">{order.title} · <bdi>{order.code}</bdi></p>}</> : <p>درخواست عمومی؛ برای گفتگوی مربوط به سفارش، از <Link prefetch={false} href="/account/orders">سفارش‌های من</Link> وارد جزئیات سفارش شوید.</p>}
    {linked && !orderLinksAvailable && <p role="status" className="notice">اتصال گفتگو به سفارش هنوز روی این سرور فعال نشده است. می‌توانید درخواست عمومی ثبت کنید.</p>}
    <label className="field">موضوع تیکت<input name="subject" required minLength={3} maxLength={160} defaultValue={consultation ? "مشاوره: " + consultation : ""} placeholder="مثلاً راهنمای آماده‌سازی فایل چاپ" /></label>
    <label className="field">متن درخواست<textarea name="message" required minLength={10} maxLength={5000} rows={6} placeholder="جزئیات را بنویسید تا دقیق‌تر راهنمایی‌تان کنیم." /></label>
    {error && <p className="error-message" role="alert">{error}</p>}
    <button className="button" disabled={busy || (linked && !orderLinksAvailable)}><Send size={17}/>{busy ? "در حال ارسال…" : "ارسال تیکت"}</button>
  </form>;
}
export default function CustomerTickets({ order, consultation }: { order?: RelatedOrder; consultation?: string }) {
  const [pageSize,setPageSize]=useState(20);
  const [page,setPage] = useState(1);
  const { data, loading, error, reload } = useApiResource<TicketPage>("tickets?page=" + page + "&pageSize="+pageSize, true);
  return <div className="tickets-layout"><section className="panel ticket-list"><div className="panel-title"><h2>پشتیبانی من</h2></div>
    {loading && <p role="status">در حال دریافت گفتگوها…</p>}{error && <p role="alert" className="error-message">{error}<button onClick={reload}>تلاش دوباره</button></p>}
    {data && <><TicketTable items={data.items} busy={loading} reload={reload}/><PageSizeControl value={pageSize} change={n=>{setPageSize(n);setPage(1);}}/>
          <Pagination className="user-pagination" page={page} pageSize={data.pageSize} total={data.total} noun="گفتگو" loading={loading} onPageChange={setPage} /></>}
  </section>{data && (data.readOnly ? <p role="status">ارسال پیام پس از فعال‌سازی نسخهٔ جدید پشتیبانی در دسترس است.</p> : <TicketComposer order={order} consultation={consultation} orderLinksAvailable={data.orderLinksAvailable}/>)}</div>;
}
