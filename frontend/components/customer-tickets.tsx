"use client";
import { PageSizeControl } from "./management-table";
import TicketTable from "./ticket-table";
import Pagination from "./pagination";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState, type FormEvent } from "react";
import { Send } from "lucide-react";
import { customerApi } from "@/lib/customer-client";
import { createRequestId } from "@/lib/request-id";
import TicketAttachments from "./ticket-attachments";
import { defaultTicketLimits, type AttachmentLimits, type TicketPage } from "@/lib/tickets";
import { useApiResource } from "@/hooks/use-api-resource";
type RelatedOrder = { id: string; title: string; code: string };
function TicketComposer({ order, consultation, limits, orderLinksAvailable = false }: { limits: AttachmentLimits; order?: RelatedOrder; consultation?: string; orderLinksAvailable?: boolean }) {
  const [busy,setBusy] = useState(false), [error,setError] = useState(""), [requestKey,setRequestKey] = useState("");
  const router=useRouter();
  const [linked,setLinked] = useState(!!order);
  const [files, setFiles] = useState<File[]>([]);
  const sending = useRef(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (sending.current || (linked && !orderLinksAvailable)) return;
    const fields = new FormData(event.currentTarget);
    const idempotencyKey = requestKey || createRequestId();
    const body = {
      subject: String(fields.get("subject") ?? ""), message: String(fields.get("message") ?? ""), idempotencyKey,
      ...(consultation ? {consultation: true} : {}), ...(linked && order ? {orderId: order.id} : {}),
    };
    sending.current = true;
    setRequestKey(idempotencyKey);
    setBusy(true);
    setError("");
    try {
      let path = "tickets";
      let payload: FormData | string = JSON.stringify(body);
      if (files.length) {
        path += "/attachments";
        const form = new FormData();
        for (const [key, value] of Object.entries(body)) form.set(key, String(value));
        for (const file of files) form.append("files", file);
        payload = form;
      }
      const result = await customerApi(path, {method:"POST", body:payload});
      router.push("/tickets/" + result.id);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "ارسال درخواست انجام نشد. دوباره تلاش کنید.");
      sending.current = false;
      setBusy(false);
    }
  }
  function changed() { setError(""); setRequestKey(""); }
  return <form id="new-ticket" className="panel ticket-composer" aria-busy={busy} onSubmit={submit}>
    <div className="panel-title"><h2>تیکت جدید</h2><span>پرسش یا درخواستت را اینجا بنویس.</span></div>
    {consultation && <p className="notice">درخواست مشاوره دربارهٔ {consultation}؛ پاسخ در همین حساب قابل پیگیری است.</p>}
    {order ? <><label className="field">نوع درخواست<select disabled={busy} value={linked ? "order" : "general"} onChange={event=>{setLinked(event.target.value === "order"); changed();}}><option value="order">درباره سفارش انتخاب‌شده</option><option value="general">درخواست عمومی</option></select></label>{linked && <p className="notice">{order.title} · <bdi>{order.code}</bdi></p>}</> : <p>درخواست عمومی؛ برای گفتگوی مربوط به سفارش، از <Link prefetch={false} href="/account/orders">سفارش‌های من</Link> وارد جزئیات سفارش شوید.</p>}
    {linked && !orderLinksAvailable && <p role="status" className="notice">اتصال گفتگو به سفارش هنوز روی این سرور فعال نشده است. می‌توانید درخواست عمومی ثبت کنید.</p>}
    <label className="field">موضوع تیکت<input onChange={changed} disabled={busy} name="subject" required minLength={3} maxLength={160} defaultValue={consultation ? "مشاوره: " + consultation : ""} placeholder="مثلاً راهنمای آماده‌سازی فایل چاپ" /></label>
    <label className="field">متن درخواست<textarea onChange={changed} disabled={busy} name="message" required minLength={10} maxLength={5000} rows={6} placeholder="جزئیات را بنویسید تا دقیق‌تر راهنمایی‌تان کنیم." /></label>
    <TicketAttachments label="پیوست فایل (اختیاری)" limits={limits} files={files} disabled={busy}
      onChange={next => {setFiles(next); changed();}} />
    {busy && <p role="status">{files.length ? "در حال بارگذاری پیوست‌ها و ثبت تیکت…" : "در حال ثبت تیکت…"}</p>}
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
  </section>{data && (data.readOnly ? <p role="status">ارسال پیام پس از فعال‌سازی نسخهٔ جدید پشتیبانی در دسترس است.</p> : <TicketComposer limits={data.attachmentLimits ?? defaultTicketLimits} order={order} consultation={consultation} orderLinksAvailable={data.orderLinksAvailable}/>)}</div>;
}
