"use client";
import { useRef,useState,type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api-client";
import { customerApi,redirectToCustomerLogin } from "@/lib/customer-client";
import { sessionFetch } from "@/lib/session-client";
import { createRequestId } from "@/lib/request-id";
import type { OrderCorrection,OrderFile,TrackingResult } from "@/lib/api-contracts";
import type { AdminOrderDetailData } from "./admin-order-detail";

const labels:Record<OrderCorrection["state"],string>={requested:"در انتظار اصلاح مشتری",submitted:"در انتظار بازبینی چاپخانه",approved:"تأییدشده",rejected:"ردشده",superseded:"با درخواست جدید جایگزین شده"};
const time=(value:string)=>new Date(value).toLocaleString("fa-IR",{timeZone:"Asia/Tehran"});
export function CorrectionHistory({items,files,admin=false}:{items:OrderCorrection[];files:OrderFile[];admin?:boolean}) {
  if(!items.length)return null;
  const link=(id?:string|null)=>{const file=files.find(f=>f.id===id);return file?<a href={(admin?"/api/admin/files/":"/api/customer/files/")+file.id+"?preview=true"} target="_blank" rel="noopener noreferrer" aria-label={"نمایش "+file.name+" (در برگهٔ جدید)"}>{file.name}{file.fileVersion?" · نسخهٔ "+file.fileVersion.toLocaleString("fa-IR"):""}</a>:null;};
  return <section className="correction-history" aria-label="تاریخچهٔ اصلاحیه‌ها"><h3>تاریخچهٔ اصلاحیه‌ها</h3><ol>{[...items].reverse().map(c=><li key={c.id} data-correction-id={c.id}>
    <details open={c.id===items.at(-1)?.id}><summary className="correction-heading"><strong>اصلاحیهٔ {c.revision.toLocaleString("fa-IR")}</strong><span className={"status-badge correction-"+c.state}>{labels[c.state]}</span></summary>
    <p className="correction-text">{c.requestText}</p><p>{c.requiresNewPdf?"ارسال PDF اصلاح‌شده الزامی است؛ فایل قبلی کافی نیست.":"PDF جدید برای این اصلاحیه اختیاری است."}</p>
    {c.baseFileId && <p>فایل مورد بررسی: {link(c.baseFileId)}</p>}
    {c.requestedAt?<small>درخواست: <time dateTime={c.requestedAt}>{time(c.requestedAt)}</time></small>:<small>درخواست قدیمی؛ زمان و نویسنده ثبت نشده است.</small>}
    {c.submittedAt&&<div className="correction-submission"><strong>پاسخ مشتری</strong><p className="correction-text">{c.submissionText}</p>{link(c.fileId)}{c.submittedAt&&<small>ارسال: {time(c.submittedAt)}</small>}</div>}
    {c.reviewedAt&&<div className="correction-review"><strong>نتیجهٔ بازبینی: {labels[c.state]}</strong>{c.reviewReason&&<p className="correction-text">{c.reviewReason}</p>}<small>{time(c.reviewedAt)}</small>{c.state==="approved"&&c.approvedFileId&&<p>فایل تأییدشده: {link(c.approvedFileId)}</p>}</div>}
  </details></li>)}</ol></section>;
}

export function AdminCorrections({data,opened,onSaved}:{data:AdminOrderDetailData;opened:boolean;onSaved:()=>Promise<void>}) {
  const [message,setMessage]=useState(""),[requestReason,setRequestReason]=useState(""),[reason,setReason]=useState("");
  const [busy,setBusy]=useState(false),[error,setError]=useState("");const lock=useRef(false),keys=useRef(new Map<string,string>());
  const order=data.order,latest=data.corrections?.at(-1);
  async function act(event:FormEvent,decision?:"approve"|"reject") {
    event.preventDefault();if(!opened||lock.current)return;
    const payload=decision?{expectedVersion:order.version,revisionId:latest?.id,revisionVersion:latest?.version,decision,reason}:{expectedVersion:order.version,message,reason:requestReason,requiresFile:true};
    const fingerprint=JSON.stringify(payload),key=keys.current.get(fingerprint)||createRequestId();keys.current.set(fingerprint,key);
    lock.current=true;setBusy(true);setError("");
    try{await api("admin/orders/"+order.id+(decision?"/corrections/review":"/corrections"),{method:"POST",body:JSON.stringify({...payload,idempotencyKey:key})});setMessage("");setReason("");setRequestReason("");await onSaved();}
    catch(cause){setError(cause instanceof Error?cause.message:"اصلاحیه ثبت نشد.");}
    finally{lock.current=false;setBusy(false);}
  }
  return <section className="order-corrections" aria-label="بررسی اصلاحیه‌ها" aria-busy={busy}>
    {!data.correctionsAvailable&&<p className="notice">گردش اصلاحیه هنوز روی این سرور فعال نشده است؛ migration 008 نیاز به اجرای جداگانه دارد.</p>}
    {data.correctionsAvailable&&["review","awaiting_customer_approval","confirmed"].includes(order.status)&&<>
      {latest?.state==="submitted"&&<form className="correction-review-form" onSubmit={e=>void act(e,"reject")}><h3>بازبینی اصلاحیهٔ مشتری</h3><p>{latest.submissionText}</p>
        {latest.fileId&&<a className="text-link" href={"/api/admin/files/"+latest.fileId+"?preview=true"} target="_blank" rel="noopener noreferrer">مشاهدهٔ PDF اصلاح‌شده</a>}
        <label className="field">توضیح بازبینی / دلیل رد<textarea maxLength={900} value={reason} onChange={e=>setReason(e.target.value)} disabled={busy}/></label>
        <button className="button button-outline danger" disabled={!opened||busy||reason.trim().length<2}>رد اصلاحیه</button><small>برای رد اصلاحیه، دلیل روشن بنویسید.</small>
      </form>}
      <details key={latest?.id || "first-correction"}><summary>درخواست اصلاح فایل</summary>
      <form className="request-information-form" onSubmit={e=>void act(e)}>
        <label className="field">دلیل اصلاح فایل<input required minLength={2} maxLength={200} value={requestReason} onChange={e=>setRequestReason(e.target.value)}/></label>
        <label className="field">شرح اصلاحات مورد نیاز<textarea required minLength={2} maxLength={650} value={message} onChange={e=>setMessage(e.target.value)} disabled={busy}/></label>
        <p>درخواست جدید، آمادگی چاپ را متوقف می‌کند. فایل‌ها و نتایج بازبینی قبلی حفظ می‌شوند.</p>
        <button className="button button-outline" disabled={!opened||busy}>ثبت درخواست اصلاح</button>
      </form></details>
    </>}
    {error&&<p role="alert" className="error-message">{error}</p>}
    <CorrectionHistory items={data.corrections||[]} files={data.files} admin/>
  </section>;
}

export function CustomerCorrections({result}:{result:TrackingResult}) {
  const router=useRouter(),latest=result.corrections?.at(-1),order=result.order;
  const [message,setMessage]=useState(""),[busy,setBusy]=useState(false),[error,setError]=useState("");
  const lock=useRef(false),keys=useRef(new Map<string,string>());
  const currentFiles=(result.files||[]).filter(f=>f.revisionId===latest?.id);
  async function upload(file:File){
    if(lock.current||!latest)return;
    if(file.size>15*1024*1024||file.type!=="application/pdf"){setError("فقط PDF تا ۱۵ مگابایت مجاز است.");return;}
    lock.current=true;setBusy(true);setError("");
    try {
      const response=await sessionFetch("/api/orders/"+order.id+"/files?revisionId="+latest.id,{method:"POST",headers:{"Content-Type":"application/pdf","x-order-code":order.code,"x-file-name":encodeURIComponent(file.name)},body:file});
      const body=await response.json();if(response.status===401){redirectToCustomerLogin();return;}if(!response.ok)throw new Error(body.error||"بارگذاری انجام نشد.");router.refresh();
    }catch(cause){setError(cause instanceof Error?cause.message:"ارسال PDF انجام نشد.");}
    finally{lock.current=false;setBusy(false);}
  }
  async function submit(event:FormEvent){
    event.preventDefault();if(lock.current||!latest)return;
    const payload={expectedVersion:order.version,revisionId:latest.id,revisionVersion:latest.version,message,fileId:currentFiles.at(-1)?.id||""};
    const fingerprint=JSON.stringify(payload),key=keys.current.get(fingerprint)||createRequestId();keys.current.set(fingerprint,key);
    lock.current=true;setBusy(true);setError("");
    try{await customerApi("customer/orders/"+order.id+"/corrections/submit",{method:"POST",body:JSON.stringify({...payload,idempotencyKey:key})});setMessage("");router.refresh();}
    catch(cause){setError(cause instanceof Error?cause.message:"ارسال اصلاحیه انجام نشد.");}
    finally{lock.current=false;setBusy(false);}
  }
  return <section className="order-corrections" aria-label="اصلاحیه‌های سفارش">
    {latest?.state==="requested"&&result.correctionsAvailable&&<form className="customer-correction-form" onSubmit={submit} aria-busy={busy}><h3>ارسال اصلاحیه</h3><p className="notice">{latest.requestText}</p>
      <p>{latest.requiresNewPdf?"ارسال PDF اصلاح‌شده الزامی است. یک فایل جدید برای همین اصلاحیه بارگذاری کنید.":"برای این اصلاحیه PDF جدید اختیاری است؛ الزامات اصلی سفارش همچنان معتبر است."}</p>
      <label className="field">فایل PDF اصلاح‌شده<input type="file" accept="application/pdf,.pdf" disabled={busy||currentFiles.length>=5} onChange={e=>{const f=e.target.files?.[0];if(f)void upload(f);e.target.value="";}}/></label>
      {currentFiles.length>0&&<p role="status">PDF این اصلاحیه بارگذاری شد: {currentFiles.at(-1)?.name}</p>}
      {currentFiles.length>=5&&<p>ظرفیت این نوبت اصلاح تکمیل است.</p>}
      <label className="field">توضیح اصلاحات انجام‌شده<textarea maxLength={3000} value={message} onChange={e=>setMessage(e.target.value)} disabled={busy}/></label>
      <button className="button" disabled={busy||(latest.requiresNewPdf&&!currentFiles.length)}>ارسال برای بازبینی چاپخانه</button>
    </form>}
    {latest?.state==="submitted"&&<p className="notice" role="status">اصلاحیهٔ شما دریافت شد و منتظر بازبینی چاپخانه است؛ هنوز برای چاپ تأیید نشده است.</p>}
    {latest?.state==="rejected"&&<p className="notice">اصلاحیه تأیید نشده است. دلیل را در تاریخچه ببینید و منتظر درخواست اصلاح جدید چاپخانه بمانید.</p>}
    {order.information_request&&!result.correctionsAvailable&&<p className="notice">درخواست اصلاح ثبت شده است، اما گردش اصلاحیه هنوز روی این سرور فعال نشده است.</p>}
    {error&&<p className="error-message" role="alert">{error}</p>}
    <CorrectionHistory items={result.corrections||[]} files={result.files||[]}/>
  </section>;
}
