"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/api-client";
import { formatToman } from "@/lib/money";
import type { CatalogItem } from "@/lib/print-catalog";
import CatalogSelect from "./catalog-select";
import TomanInput from "./toman-input";

const groups = [
  ["چاپ متن", [["bw","چاپ هر صفحه سیاه و سفید"],["color","چاپ هر صفحه رنگی"]]],
  ["کاغذ متن", [["cream","افزایش قیمت کاغذ کرم، هر صفحه"],["coated","افزایش قیمت گلاسه، هر صفحه"]]],
  ["جلد و صحافی", [["coverPaper","کاغذ جلد گلاسه ۲۵۰ گرم، هر جلد"],["coverPrint","چاپ جلد چهار رنگ یک‌رو، هر جلد"],["soft","صحافی شومیز، هر جلد"],["hard","جلد سخت، هر جلد"],["spiral","صحافی فنری، هر جلد"],["lamination","سلفون جلد، هر جلد"]]],
  ["آماده‌سازی", [["setup","هزینه ثابت آماده‌سازی هر سفارش"]]],
] as const;
type Revision = {version:number; updatedAt:string|null; rates:Record<string,number>|null};
const fieldKeys = groups.flatMap(([, fields]) => fields.map(([key]) => key));
export default function CutTariffEditor({legacy}: {legacy:Partial<Record<string,number>>}) {
  const [cuts,setCuts]=useState<CatalogItem[]>([]), [id,setId]=useState(""), [load,setLoad]=useState(0);
  const [revision,setRevision]=useState<Revision|null>(null), [fields,setFields]=useState<Record<string,string>>({});
  const [message,setMessage]=useState(""), [error,setError]=useState(""), [busy,setBusy]=useState(false);
  const dirty=!!revision && fieldKeys.some(k => (fields[k] ?? "") !== String(revision.rates?.[k] ?? ""));
  useEffect(() => {
    const ac=new AbortController();
    api<{items:CatalogItem[]}>("admin/print-catalog",{signal:ac.signal}).then(result=>{
      if(!ac.signal.aborted) setCuts(result.items.filter(i=>i.active && i.kind==="bookSizes"));
    }).catch(e=>{if(!ac.signal.aborted)setError(e.message);});
    return ()=>ac.abort();
  },[load]);
  useEffect(()=>{
    if(!id)return;
    const ac=new AbortController();
    api<Revision>("admin/print-tariffs/"+id,{signal:ac.signal}).then(row=>{
      if(ac.signal.aborted)return;
      setRevision(row);setFields(Object.fromEntries(fieldKeys.map(k=>[k,String(row.rates?.[k] ?? "")])));
    }).catch(e=>{if(!ac.signal.aborted)setError(e.message);});
    return ()=>ac.abort();
  },[id,load]);
  useEffect(()=>{
    if(!dirty)return;
    const warn=(e:BeforeUnloadEvent)=>e.preventDefault();
    window.addEventListener("beforeunload",warn);return ()=>window.removeEventListener("beforeunload",warn);
  },[dirty]);
  function select(next:string) {
    if (busy || next===id || (dirty && !window.confirm("تغییرات این قطع ذخیره نشده است. قطع تغییر کند؟"))) return;
    setFields({});setRevision(null);setMessage("");setError("");setId(next);
  }
  async function save(){
    if(!revision || !id || busy) return;
    setMessage("");setError("");
    if(fieldKeys.some(k=>!/^\d+$/.test(fields[k]??"") || Number(fields[k])>10_000_000)){setError("همهٔ مبلغ‌ها را بین صفر و ده میلیون تومان وارد کنید.");return;}
    setBusy(true);
    try{
      const row=await api<Revision>("admin/print-tariffs/"+id,{method:"PUT",body:JSON.stringify({version:revision.version,rates:Object.fromEntries(fieldKeys.map(k=>[k,Number(fields[k])]))})});
      setRevision(row);setFields(Object.fromEntries(fieldKeys.map(k=>[k,String(row.rates?.[k]??"")])));setMessage("تعرفهٔ این قطع ذخیره شد.");
    }catch(e){setError(e instanceof Error?e.message:"ذخیره انجام نشد.");}finally{setBusy(false);}
  }
  return <section className="cut-tariff-panel panel" aria-label="تعرفه چاپ بر اساس قطع">
    <h3>تعرفهٔ چاپ بر اساس قطع کتاب</h3>
    <CatalogSelect placeholder="یک قطع انتخاب کنید" label="انتخاب قطع کتاب" value={id} disabled={busy} onChange={select} options={cuts.map(c=>({value:c.id,label:c.label,description:c.metadata.isCustom ? "ابعاد سفارشی" : String(c.metadata.width)+" × "+String(c.metadata.height)+" سانتی‌متر"}))}/>
    {fieldKeys.some(k=>legacy[k]!=null) && <details className="legacy-tariffs"><summary>مشاهدهٔ نرخ‌های قدیمی بدون قطع</summary>
      <p>این مبالغ سابقهٔ نرخ‌های عمومی هستند. نرخ هر قطع را پس از بررسی جداگانه ثبت کنید.</p>
      <dl>{groups.flatMap(([,entries])=>entries.map(([key,label])=>legacy[key]==null ? null : <div key={key}><dt>{label}</dt><dd>{formatToman(legacy[key])}</dd></div>))}</dl></details>}
    {id && !revision && !error && <p role="status">در حال دریافت تعرفه…</p>}
    {revision && <><p role="status">{revision.version ? "نسخه "+revision.version.toLocaleString("fa-IR")+" · "+new Date(revision.updatedAt!).toLocaleString("fa-IR") : "تعرفهٔ این قطع هنوز تنظیم نشده است."}{dirty && " · تغییرات ذخیره‌نشده"}</p>
      {groups.map(([label,items])=><fieldset key={label}><legend>{label}</legend><div className="form-grid">{items.map(([k,text])=><label className="field" key={k}>{text}<TomanInput aria-label={text} value={fields[k]??""} change={v=>{setFields(prev=>({...prev,[k]:v}));setMessage("");}} disabled={busy}/><small>تومان</small></label>)}</div></fieldset>)}
      <button type="button" className="button" disabled={busy||!dirty} onClick={save}>{busy?"در حال ذخیره…":"ذخیره تعرفه این قطع"}</button></>}
    {message && <p role="status">{message}</p>}{error && <div role="alert" className="error-message">{error} <button type="button" onClick={()=>{if(dirty&&!window.confirm("تعرفه دوباره بارگذاری شود؟"))return;setError("");setRevision(null);setLoad(n=>n+1);}}>دریافت دوباره</button></div>}
  </section>;
}
