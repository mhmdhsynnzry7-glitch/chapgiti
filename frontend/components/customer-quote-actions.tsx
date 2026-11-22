"use client";
import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { customerApi } from "@/lib/customer-client";
import FinalConditions from "./final-conditions";
import { createRequestId } from "@/lib/request-id";
import type { TrackingResult } from "@/lib/api-contracts";

export default function CustomerQuoteActions({ result }: { result: TrackingResult }) {
  const router=useRouter();const [reason,setReason]=useState(""),[busy,setBusy]=useState(false),[error,setError]=useState("");
  const sending=useRef(false),keys=useRef(new Map<string,string>());
  const order=result.order,quote=result.quotes?.at(-1);
  const pending=order.status==="awaiting_customer_approval"&&quote?.state==="sent";

  async function act(action:string) {
    if(sending.current)return;
    if(action!=="accept"&&reason.trim().length<2){setError("دلیل یا توضیح کوتاه را وارد کنید.");return;}
    if(action==="accept"&&!window.confirm("مبلغ و شرایط این نسخه از پیشنهاد را تأیید می‌کنید؟ شرایط نهایی چاپ و تحویل جداگانه برای شما ارسال می‌شود."))return;
    const payload={expectedVersion:order.version,quoteId:quote?.id,quoteVersion:quote?.version,reason};
    const fingerprint=action+JSON.stringify(payload);const key=keys.current.get(fingerprint)||createRequestId();keys.current.set(fingerprint,key);
    sending.current=true;setBusy(true);setError("");
    try {await customerApi("customer/orders/"+order.id+"/"+("quote/"+action),{method:"POST",body:JSON.stringify({...payload,idempotencyKey:key})});setReason("");router.refresh();}
    catch(cause){setError(cause instanceof Error?cause.message:"ثبت تصمیم انجام نشد.");}
    finally{sending.current=false;setBusy(false);}
  }
  if(!pending)return quote?.state==="accepted" && order.version ? <FinalConditions id={order.id} version={order.version} terms={result.finalConditions} amount={order.final_price} fileId={result.approvedFileId} files={result.files||[]} editable={order.status==="confirmed"} refresh={()=>router.refresh()}/> : null;
  return <section className="customer-quote-actions" aria-label="اقدام مورد نیاز شما" aria-busy={busy}>
    <h3>تصمیم شما دربارهٔ پیشنهاد</h3>
    <label className="field">دلیل اصلاح یا انصراف<textarea maxLength={900} value={reason} disabled={busy} onChange={e=>setReason(e.target.value)} /></label>
    {error && <p className="error-message" role="alert">{error}<button className="text-link" type="button" onClick={()=>router.refresh()}>تازه‌سازی درخواست</button></p>}
    <div className="order-workflow-actions"><button className="button" disabled={busy} onClick={()=>void act("accept")}>تأیید قیمت</button><button className="button button-outline" disabled={busy} onClick={()=>void act("request-revision")}>درخواست بازنگری قیمت</button><button className="button button-outline danger" disabled={busy} onClick={()=>void act("decline")}>رد پیشنهاد قیمت</button></div>
    {pending && <p>پس از تأیید قیمت، فایل و شرایط نهایی چاپ و تحویل را جداگانه بررسی می‌کنید.</p>}
  </section>;
}
