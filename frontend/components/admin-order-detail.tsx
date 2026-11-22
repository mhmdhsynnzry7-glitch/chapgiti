"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/api-client";
import type { OrderDetail } from "@/lib/api-contracts";
import { date, money, adminStatuses as statuses } from "@/lib/pricing";
import { formatToman } from "@/lib/money";
import TomanInput from "./toman-input";
import { parseApprovedAmount } from "@/lib/amount-input";
import { createRequestId } from "@/lib/request-id";
import BookSpecificationSummary from "./book-specification-summary";
import { Status } from "./ui";
import FinalConditions from "./final-conditions";
import CopyOrderCode from "./copy-order-code";
import { ActionSummary } from "./order-attention";
import OrderProgress, {nextOrderAction} from "./order-progress";
import AdminQuotePanel from "./admin-quote-panel";
import type { OrderQuote } from "@/lib/api-contracts";
import type { OrderAttention,ProductionReadiness } from "@/lib/api-contracts";
import { AdminCorrections } from "./order-corrections";
import { OrderSeen } from "./notification-center";

export type AdminOrderDetailData = OrderDetail & OrderAttention & {
  allowedTransitions: string[]; notesAvailable: boolean;
  quotesAvailable?: boolean; quotes?: OrderQuote[];
  conversations?: { id:string; subject:string; status:string }[];
  internalNotes: { id: string; note: string; actor: string; created_at: string }[];
  productionReadiness?: ProductionReadiness;
};
const time = (value: string) => new Date(value).toLocaleString("fa-IR", { timeZone: "Asia/Tehran" });

function eventAuthor(actor: string | null | undefined, customer: string) {
  if (!actor?.trim()) return "ثبت‌کننده مشخص نیست";
  if (actor === "customer" || actor.startsWith("customer:")) return customer || "مشتری";
  if (actor === "admin" || actor.startsWith("admin:") || actor.includes("@")) return "مدیر چاپخانه";
  return "سامانه";
}

export default function AdminOrderDetail({ id, back, notify, initialData }: { id: string; back: () => void; notify: (text: string, error?: boolean) => void; initialData?: AdminOrderDetailData }) {
  const [data, setData] = useState<AdminOrderDetailData | null>(initialData ?? null), [error, setError] = useState("");
  const [opened, setOpened] = useState(false);
  const [busy, setBusy] = useState(false), [revision, setRevision] = useState(0);
  const [price, setPrice] = useState(""), [reason, setReason] = useState(""), [note, setNote] = useState("");
  const [noteKey, setNoteKey] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    api<AdminOrderDetailData>("admin/orders/" + id + "/open", { method: "POST", body: "{}", signal: controller.signal })
      .then(result => { if (!controller.signal.aborted) { setData(result); setPrice(new Intl.NumberFormat("en-US").format(result.order.final_price ?? result.order.estimate)); setError(""); setOpened(true); } })
      .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "دریافت سفارش انجام نشد."); });
    return () => controller.abort();
  }, [id, revision]);
  async function transition(status: string) {
    if (!opened || !data || busy) return;
    const proposalWorkflow = (data.order.workflow_version ?? 1) >= 2;
    const approving = status === "printing" && !proposalWorkflow;
    const amount = parseApprovedAmount(price);
    if (approving && amount == null) { setError("قیمت تأییدشده باید عدد صحیح بین صفر و ۱۰۰٬۰۰۰٬۰۰۰٬۰۰۰ تومان باشد."); return; }
    if (status === "cancelled" && !reason.trim()) { setError("دلیل رد سفارش را وارد کنید."); return; }
    if (!window.confirm(status === "cancelled" ? "سفارش با دلیل واردشده رد شود؟" : approving ? "سفارش با قیمت " + formatToman(amount) + " تأیید و وارد چاپ شود؟" : "وضعیت سفارش به «" + statuses[status] + "» تغییر کند؟")) return;
    setBusy(true); setError("");
    try {
      await api("admin/orders/" + id + (proposalWorkflow && status === "printing" ? "/start-printing" : ""), { method: proposalWorkflow && status === "printing" ? "POST" : "PATCH", body: JSON.stringify({ status, version: data.order.version, idempotencyKey: createRequestId(), ...(approving ? { finalPrice: amount } : {}), note: status === "cancelled" ? reason : "" }) });
      setData(await api<AdminOrderDetailData>("admin/orders/" + id)); setReason(""); notify("وضعیت سفارش ثبت شد.");
    } catch (cause) { setError(cause instanceof Error ? cause.message : "تغییر وضعیت انجام نشد."); }
    finally { setBusy(false); }
  }
  async function saveNote(event: React.FormEvent) {
    event.preventDefault(); if (!opened || busy || !data) return;
    setBusy(true); setError(""); const key = noteKey || createRequestId(); setNoteKey(key);
    try {
      await api("admin/orders/" + id + "/notes", { method: "POST", body: JSON.stringify({ idempotencyKey: key, note }) });
      setData(await api<AdminOrderDetailData>("admin/orders/" + id)); setNote(""); setNoteKey(""); notify("یادداشت اجرایی ثبت شد.");
    } catch (cause) { setError(cause instanceof Error ? cause.message : "یادداشت ثبت نشد."); }
    finally { setBusy(false); }
  }
  if (!data) return <section className="panel"><button className="text-link" onClick={back}>بازگشت به سفارش‌ها</button>{error ? <p role="alert">{error}<button onClick={() => setRevision(value => value + 1)}>تلاش دوباره</button></p> : <p role="status">در حال بازکردن سفارش…</p>}</section>;
  const order = data.order, proposalWorkflow = (order.workflow_version ?? 1) >= 2;
  const canApprove = opened && !proposalWorkflow && !!data.productionReadiness?.canStart && data.allowedTransitions.includes("printing");
  const primaryFileId = order.spec.primaryFileId ?? data.files.filter(file => !file.revisionId).at(-1)?.id;
  const correction = data.corrections?.at(-1);
  const reviewedFileId = correction?.fileId ?? correction?.baseFileId ?? primaryFileId;
  const accepted = data.quotes?.some(q => q.state === "accepted");
  const reviewable = !correction || ["submitted", "approved"].includes(correction.state);
  const actionLabels: Record<string,string> = { review: "شروع بررسی", printing: "تأیید و شروع چاپ", binding: "پایان چاپ و ارسال به صحافی", ready: order.status === "printing" ? "پایان چاپ و آماده تحویل" : "پایان صحافی و آماده تحویل", completed: "ثبت تحویل سفارش", cancelled: "رد سفارش" };
  if (proposalWorkflow) actionLabels.printing = "شروع چاپ";
  return <div className="order-editor">
    {opened&&<OrderSeen orderId={id} sequence={data.viewSequence} available={data.orderViewsAvailable}/>}
    <div className="admin-heading"><div><h2>{order.title}</h2><p>{order.customer?.trim() || order.phone} · <CopyOrderCode code={order.code}/></p></div><button className="button button-outline button-sm" onClick={back}>بازگشت به سفارش‌ها</button></div>
    {error && <p className="error-message" role="alert">{error}<button className="text-link" onClick={() => { setOpened(false); setRevision(value => value + 1); }}>تازه‌سازی سفارش</button></p>}
    <div className="admin-grid"><section className="panel">
      <div className="panel-title"><h3>مشخصات سفارش</h3><Status value={order.status} label={statuses[order.status]} /></div>
      <dl className="order-information"><div><dt>مشتری</dt><dd>{order.customer?.trim() || order.phone}</dd></div><div><dt>موبایل</dt><dd><bdi>{order.phone}</bdi></dd></div><div><dt>تاریخ ثبت</dt><dd>{date(order.created_at)}</dd></div><div><dt>صفحات / تیراژ</dt><dd>{order.spec.pages == null ? "ثبت نشده" : money(order.spec.pages)} / {order.spec.quantity == null ? "ثبت نشده" : money(order.spec.quantity)}</dd></div></dl>
      <BookSpecificationSummary spec={order.spec} />
      {order.spec.customerNotes && <p>{order.spec.customerNotes}</p>}
      <h3 id="order-files">فایل‌های سفارش</h3>{data.files.length ? <ul className="order-files">{data.files.map(file => <li key={file.id}><a href={"/api/admin/files/" + file.id}>{file.name}</a>{" · "}<a href={"/api/admin/files/"+file.id+"?preview=true"} target="_blank" rel="noopener noreferrer" aria-label={"نمایش "+file.name+" در برگهٔ جدید"}>نمایش</a>{file.fileVersion&&<span> · نسخهٔ {file.fileVersion.toLocaleString("fa-IR")}</span>}{order.approved_file_id===file.id&&<strong> · فایل تأییدشدهٔ چاپ</strong>} · <bdi>{money(Math.ceil(file.size / 1024))} KB</bdi></li>)}</ul> : <p>فایلی ارسال نشده است.</p>}
      {order.spec.priceBreakdown && <section className="admin-cost-breakdown"><h3>جزئیات برآورد ثبت‌شده</h3>{Object.entries({ print: "چاپ متن هر جلد", binding: "صحافی هر جلد", cover: "جلد هر جلد", extras: "خدمات تکمیلی هر جلد", setup: "آماده‌سازی", discountAmount: "تخفیف", total: "برآورد کل" }).map(([key,label]) => {
        const value = order.spec.priceBreakdown?.[key as keyof NonNullable<typeof order.spec.priceBreakdown>];
        return typeof value === "number" ? <div className="quote-row" key={key}><span>{label}</span><b>{formatToman(value)}</b></div> : null;
      })}</section>}
    </section><section className="panel">
      <h3>بررسی و اجرای سفارش</h3>{order.attention ? <ActionSummary state={order.attention}/> : <p className="notice">{nextOrderAction(order.status, correction?.state, data.finalConditions, data.quotes?.at(-1)?.state)}</p>}<dl className="order-price-totals"><div><dt>قیمت برآوردی سیستم</dt><dd>{formatToman(order.estimate)}</dd></div><div><dt>قیمت تأییدشده</dt><dd>{formatToman(order.final_price)}</dd></div></dl>
      <OrderProgress terms={data.finalConditions} binding={!order.spec.details?.printOnly} status={order.status} accepted={data.quotes?.some(q=>q.state==="accepted")}/>
      {accepted && <FinalConditions admin id={id} version={order.version} terms={data.finalConditions} amount={order.final_price} fileId={order.approved_file_id} files={data.files} reviewedFileId={reviewedFileId}
       editable={opened && reviewable && ["confirmed","review"].includes(order.status)} refresh={()=>setRevision(n=>n+1)}/>}
      <AdminQuotePanel data={data} opened={opened} onSaved={async()=>{setData(await api<AdminOrderDetailData>("admin/orders/"+id));notify("درخواست به‌روز شد.");}} />
      <AdminCorrections data={data} opened={opened} onSaved={async()=>{setData(await api<AdminOrderDetailData>("admin/orders/"+id));notify("نتیجهٔ اصلاحیه ثبت شد.");}}/>
      {canApprove && <label className="field">قیمت تأییدشده (تومان)<TomanInput value={price} change={setPrice} disabled={busy || (order.status === "confirmed" && order.final_price != null)} /><small>مبلغ پیشنهادی از برآورد سیستم است و فقط با تأیید شما ذخیره می‌شود.</small></label>}
      {data.allowedTransitions.includes("cancelled") && <label className="field">دلیل رد سفارش<textarea aria-label="دلیل رد سفارش" maxLength={900} value={reason} onChange={event => setReason(event.target.value)} /></label>}
      {!opened && !error && <p role="status">در حال بازکردن سفارش…</p>}
      {data.productionReadiness&&!data.productionReadiness.canStart&&order.status==="confirmed" && !!data.finalConditions?.acceptedAt&&<div className="notice production-blockers"><strong>پیش‌نیازهای شروع چاپ</strong><ul>{data.productionReadiness.problems.map((p,i)=><li key={p.code+i}>{p.message}</li>)}</ul></div>}
      <div className="order-workflow-actions">{data.allowedTransitions.filter(status=>status!=="printing"||data.productionReadiness?.canStart).map(status => <button key={status} className={status === "cancelled" ? "button button-outline danger" : "button"} disabled={!opened || busy || (status==="printing"&&!!data.productionReadiness&&!data.productionReadiness.canStart&&(!canApprove||data.productionReadiness.problems.some(p=>p.code!=="FINAL_PRICE_REQUIRED")))} onClick={() => void transition(status)}>{actionLabels[status]}</button>)}</div>
      {!data.allowedTransitions.length && ["awaiting_customer_approval","completed","cancelled"].includes(order.status) && <p className="notice">{order.status === "awaiting_customer_approval" ? "منتظر تصمیم مشتری؛ شروع چاپ تا تأیید او مجاز نیست." : "این سفارش به وضعیت پایانی رسیده است."}</p>}
      <h3>تاریخچهٔ سفارش</h3><ol className="timeline">{data.events.map(event => <li key={event.id}><strong>{statuses[event.status]}</strong><p>{event.note}</p><small>{time(event.created_at)} · {eventAuthor(event.actor, order.customer?.trim() || order.phone)}</small></li>)}</ol>
    </section></div>
    <section className="panel"><h3>پشتیبانی این سفارش</h3>
      {data.conversations?.length ? <ul>{data.conversations.map(t=><li key={t.id}><a href={"/admin/tickets/"+t.id}>{t.subject}</a></li>)}</ul> : <p>هنوز گفت‌وگویی برای این سفارش ثبت نشده است.</p>}
    </section>
    <section className="panel operational-notes"><h3>یادداشت اجرایی سفارش</h3><p>اختیاری و فقط قابل مشاهده برای مدیریت چاپخانه.</p>
      {order.admin_notes && <article className="notice"><p>{order.admin_notes}</p><small>یادداشت قبلی؛ زمان و نویسندهٔ آن ثبت نشده است.</small></article>}
      {data.internalNotes.map(item => <article className="operational-note" key={item.id}><p>{item.note}</p><small>{time(item.created_at)} · {item.actor?.includes("@") ? "مدیر چاپخانه" : "سامانه"}</small></article>)}
      {data.notesAvailable ? <form onSubmit={saveNote}><label className="field">یادداشت جدید<textarea name="operationalNote" required maxLength={5000} value={note} onChange={event => { setNote(event.target.value); setNoteKey(""); }} /></label><button className="button" disabled={!opened || busy}>ثبت یادداشت اجرایی</button></form> : <p className="notice">ثبت تاریخچهٔ یادداشت اجرایی هنوز روی این سرور فعال نشده است. یادداشت قبلی حفظ شده است.</p>}
    </section>
  </div>;
}
