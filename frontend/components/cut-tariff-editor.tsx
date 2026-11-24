"use client";
import { useEffect, useRef, useState } from "react";
import { api } from "@/lib/api-client";
import type { CatalogItem } from "@/lib/print-catalog";
import CatalogSelect from "./catalog-select";
import TomanInput from "./toman-input";

const groups = [
  ["چاپ متن", [["bw","چاپ هر صفحه سیاه و سفید"],["color","چاپ هر صفحه رنگی"]]],
  ["کاغذ متن", [["cream","افزایش قیمت کاغذ کرم، هر صفحه"],["coated","افزایش قیمت گلاسه، هر صفحه"]]],
  ["جلد و صحافی", [["coverPaper","کاغذ جلد گلاسه ۲۵۰ گرم، هر جلد"],["coverPrint","چاپ جلد چهار رنگ یک‌رو، هر جلد"],["soft","صحافی شومیز، هر جلد"],["hard","جلد سخت، هر جلد"],["spiral","صحافی فنری، هر جلد"],["lamination","سلفون جلد، هر جلد"]]],
  ["آماده‌سازی", [["setup","هزینه ثابت آماده‌سازی هر سفارش"]]],
] as const;
type Revision = { version: number; updatedAt: string | null; rates: Record<string, number> | null; isProposed: boolean };
const keys = groups.flatMap(([, fields]) => fields.map(([key]) => key));
const asFields = (row: Revision) => Object.fromEntries(keys.map(key => [key, String(row.rates?.[key] ?? "")]));
const dimensions = (cut: CatalogItem) => cut.metadata.isCustom ? "ابعاد سفارشی" :
  String(cut.metadata.width) + " × " + String(cut.metadata.height) + " سانتی‌متر";

export default function CutTariffEditor({onDirty}: {onDirty: (value: boolean) => void}) {
  const [cuts, setCuts] = useState<CatalogItem[]>([]);
  const [id, setId] = useState(""), [reload, setReload] = useState(0);
  const [revision, setRevision] = useState<Revision | null>(null);
  const [fields, setFields] = useState<Record<string, string>>({});
  const [message, setMessage] = useState(""), [error, setError] = useState("");
  const pending = useRef(false);
  const [operation, setOperation] = useState<"copy" | "save" | null>(null);
  const busy = operation !== null;
  const [copySource, setCopySource] = useState("");
  const valid = keys.every(key => /^\d+$/.test(fields[key] ?? "") && Number(fields[key]) <= 10_000_000);
  const cut = cuts.find(item => item.id === id);
  const dirty = !!revision && keys.some(key => !fields[key] ? fields[key] !== String(revision.rates?.[key] ?? "") : Number(fields[key]) !== revision.rates?.[key]);
  useEffect(() => { onDirty(dirty); }, [dirty, onDirty]);
  useEffect(() => {
    const request = new AbortController();
    api<{items: CatalogItem[]}>("admin/print-catalog", {signal: request.signal})
      .then(data => { if (!request.signal.aborted) setCuts(data.items.filter(i => i.active && i.kind === "bookSizes")); })
      .catch(e => { if (!request.signal.aborted) setError(e.message); });
    return () => request.abort();
  }, [reload]);
  useEffect(() => {
    if (!id) return;
    const request = new AbortController();
    api<Revision>("admin/print-tariffs/" + id, {signal: request.signal})
      .then(row => { if (!request.signal.aborted) { setRevision(row); setFields(asFields(row)); } })
      .catch(e => { if (!request.signal.aborted) setError(e.message); });
    return () => request.abort();
  }, [id, reload]);
  function select(next: string) {
    if (pending.current || next === id || (dirty && !window.confirm("تغییرات این قطع ذخیره نشده است. قطع تغییر کند؟"))) return;
    setId(next); setRevision(null); setFields({}); setCopySource(""); setMessage(""); setError("");
  }
  async function copy() {
    const source = cuts.find(item => item.id === copySource);
    if (!source || !cut || pending.current || !window.confirm("مبالغ «" + source.label + "» در فرم «" + cut.label + "» جایگزین شوند؟ برای ثبت، ذخیره را بزنید.")) return;
    pending.current = true; setOperation("copy"); setMessage(""); setError("");
    try {
      const row = await api<Revision>("admin/print-tariffs/" + source.id);
      if (!row.rates) throw new Error("برای قطع مبدأ تعرفه‌ای ثبت نشده است.");
      setFields(asFields(row)); setMessage("مبالغ در فرم کپی شد. پیش از ثبت، آن‌ها را بررسی کنید.");
    } catch (cause) { setError(cause instanceof Error ? cause.message : "دریافت تعرفه انجام نشد."); }
    finally { pending.current = false; setOperation(null); }
  }
  async function save() {
    if (!revision || !id || pending.current || (!dirty && !revision.isProposed)) return;
    setMessage(""); setError("");
    if (!valid) {
      setError("همهٔ مبلغ‌ها را بین صفر و ده میلیون تومان وارد کنید."); return;
    }
    pending.current = true; setOperation("save");
    try {
      const rates = Object.fromEntries(keys.map(key => [key, Number(fields[key])]));
      const row = await api<Revision>("admin/print-tariffs/" + id, {method:"PUT",
        body: JSON.stringify({version:revision.version, rates, approveProposal:revision.isProposed})});
      setRevision(row); setFields(asFields(row)); setMessage("تعرفهٔ این قطع ذخیره شد.");
    } catch (cause) { setError(cause instanceof Error ? cause.message : "ذخیره انجام نشد."); }
    finally { pending.current = false; setOperation(null); }
  }
  const choices = cuts.map(item => ({value: item.id, label: item.label, description: dimensions(item)}));
  return <section className="cut-tariff-panel panel" aria-label="تعرفه چاپ بر اساس قطع">
    <h3>تعرفهٔ چاپ بر اساس قطع کتاب</h3>
    <CatalogSelect placeholder="یک قطع انتخاب کنید" label="انتخاب قطع کتاب" value={id} disabled={busy} onChange={select} options={choices}/>
    {id && !revision && !error && <p role="status">در حال دریافت تعرفه…</p>}
    {revision && <>
      <p role="status">{cut && dimensions(cut)} · {revision.version
        ? "نسخه " + revision.version.toLocaleString("fa-IR") + " · " + (revision.isProposed ? "پیشنهادی، در انتظار تأیید مدیر" : "تأییدشده") + " · " + new Date(revision.updatedAt!).toLocaleDateString("fa-IR")
        : "تعرفه‌ای ثبت نشده است."}{dirty && " · تغییرات ذخیره‌نشده"}</p>
      {revision.isProposed && <p>این مبالغ از نرخ‌های مرجع چاپخانه محاسبه شده‌اند. با تأیید شما برای برآوردهای جدید فعال می‌شوند.</p>}
      {cut?.metadata.isCustom === true && <p>تعرفهٔ پایه برای ابعاد مرجع ۱۴٫۵ × ۲۱٫۵ سانتی‌متر است. هزینهٔ چاپ و جلد با مساحت واردشده محاسبه می‌شود.</p>}
      {groups.map(([label, items]) => <fieldset key={label}><legend>{label}</legend><div className="form-grid">
        {items.map(([key, title]) => <label className="field" key={key}>{title}
          <TomanInput aria-label={title} required value={fields[key] ?? ""} change={value => {setFields(old => ({...old, [key]:value})); setMessage("");}} disabled={busy}/>
          <small>تومان</small></label>)}</div></fieldset>)}
      <details><summary>کپی نرخ از قطع دیگر</summary>
        <CatalogSelect label="قطع مبدأ" placeholder="انتخاب قطع مبدأ" options={choices.filter(item => item.value !== id)} value={copySource} onChange={setCopySource} disabled={busy}/>
        <button type="button" className="button button-outline" disabled={busy || !copySource} onClick={copy}>کپی در فرم</button>
      </details>
      <div className="tariff-actions">
        <button type="button" className="button" disabled={busy || !valid || (!dirty && !revision.isProposed)} aria-busy={operation === "save"} onClick={save}>
          {operation === "save" ? "در حال ذخیره تعرفه…" : revision.isProposed ? "تأیید و فعال‌سازی تعرفه این قطع" : "ذخیره تعرفه این قطع"}</button>
        <button type="button" className="button button-outline" disabled={busy || !dirty}
          onClick={() => {setFields(asFields(revision)); setError(""); setMessage("");}}>لغو تغییرات</button>
        {!busy && !dirty && !revision.isProposed && revision.version > 0 && <small>همهٔ تغییرات ذخیره شده‌اند.</small>}
      </div></>}
    {message && <p role="status">{message}</p>}
    {error && <div role="alert" className="error-message">{error} <button type="button" onClick={() => {
      if (dirty && !window.confirm("تغییرات ذخیره‌نشده کنار گذاشته شود؟")) return;
      setError(""); setMessage(""); setRevision(null); setReload(value => value + 1);
    }}>دریافت دوباره</button></div>}
  </section>;
}
