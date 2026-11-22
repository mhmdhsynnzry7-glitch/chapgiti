"use client";
import { useRef, useState, type FormEvent } from "react";
import { api } from "@/lib/api-client";
import { createRequestId } from "@/lib/request-id";
import TomanInput from "./toman-input";
import { parseApprovedAmount } from "@/lib/amount-input";
import QuoteHistory from "./quote-history";
import type { AdminOrderDetailData } from "./admin-order-detail";

export default function AdminQuotePanel({data,opened,onSaved}:{data:AdminOrderDetailData;opened:boolean;onSaved:()=>Promise<void>}) {
  const [amount,setAmount]=useState("");
  const [scope,setScope]=useState(() => {
    const {quantity,pages}=data.order.spec;
    if (typeof quantity !== "number" || typeof pages !== "number" || quantity <= 0 || pages <= 0) return "";
    return "چاپ «"+data.order.title+"» مطابق مشخصات ثبت‌شده، "+quantity.toLocaleString("fa-IR")+" جلد، "+pages.toLocaleString("fa-IR")+" صفحه";
  });
  const [difference,setDifference]=useState(""),[exclusions,setExclusions]=useState("");
  const [busy,setBusy]=useState(false),[error,setError]=useState("");const sending=useRef(false),keys=useRef(new Map<string,string>());
  const order=data.order, correction=data.corrections?.at(-1);
  const fileId=correction?.fileId ?? correction?.baseFileId ?? order.spec.primaryFileId ?? data.files.filter(f=>!f.revisionId).at(-1)?.id;
  const reviewable=!!fileId && (!correction || ["submitted","approved"].includes(correction.state));
  async function submit(event:FormEvent) {
    event.preventDefault();if(!opened||sending.current)return;
    const value=parseApprovedAmount(amount);
    if(value==null){setError("مبلغ پیشنهادی باید عدد صحیح معتبر به تومان باشد.");return;}
    const payload={expectedVersion:order.version,amountToman:value,scopeText:scope,priceExplanation:difference,reviewedFileId:fileId,exclusionsText:exclusions};
    const fingerprint="quote"+JSON.stringify(payload);const key=keys.current.get(fingerprint)||createRequestId();keys.current.set(fingerprint,key);
    sending.current=true;setBusy(true);setError("");
    try {await api("admin/orders/"+order.id+"/quotes",{method:"POST",body:JSON.stringify({...payload,idempotencyKey:key})});await onSaved();}
    catch(cause){setError(cause instanceof Error?cause.message:"ثبت درخواست انجام نشد.");}
    finally{sending.current=false;setBusy(false);}
  }
  return <section className="admin-quote-panel">
    {!data.quotesAvailable && <p className="notice">پیشنهاد قیمت هنوز روی این سرور فعال نشده است؛ migration 006 باید جداگانه تأیید و اجرا شود.</p>}
    {data.quotesAvailable && !data.quotes?.some(quote => quote.state === "accepted") && reviewable && order.status==="review" && <form onSubmit={event=>void submit(event)} aria-busy={busy}>
      <h3>ارسال پیشنهاد قیمت</h3>
      <p>فایل این پیشنهاد · <a href={"/api/admin/files/"+fileId+"?preview=true"} target="_blank" rel="noreferrer">{data.files.find(f=>f.id===fileId)?.name}</a></p>
      <label className="field">مبلغ پیشنهادی (تومان)<TomanInput value={amount} change={setAmount} required /></label>
      <label className="field">خدمات شامل قیمت<textarea aria-label="خدمات شامل قیمت" required minLength={2} maxLength={3000} value={scope} onChange={e=>setScope(e.target.value)} /></label>
      <details><summary>توضیحات اختیاری قیمت</summary>
      <label className="field">دلیل تفاوت با برآورد<input maxLength={500} value={difference} onChange={e=>setDifference(e.target.value)} /></label>
      <label className="field">خارج از قیمت<textarea maxLength={2000} value={exclusions} onChange={e=>setExclusions(e.target.value)} /></label>
      </details>
      <p>این پیشنهاد چاپ را شروع نمی‌کند؛ مالک درخواست باید همین نسخه را تأیید کند. هیچ مبلغ یا موعدی از پیش پذیرفته نشده است.</p>
      <button className="button" disabled={!opened||busy}>ارسال پیشنهاد قیمت</button>
    </form>}
    {error && <p role="alert" className="error-message">{error}</p>}
    <QuoteHistory quotes={data.quotes||[]} />
  </section>;
}
