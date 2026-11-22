"use client";
import Link from "next/link";
import ManagementTable from "./management-table";
import SectionEditor from "./service-sections";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { ArrowUp, ArrowDown, Plus, Save } from "lucide-react";
import { api } from "@/lib/api-client";
import { sessionFetch } from "@/lib/session-client";
import { useApiResource } from "@/hooks/use-api-resource";
import { normalizeDigits } from "@/lib/pricing";
import { servicePayload, type ServiceRecord } from "@/lib/services";

type Props = { initialId?: string; notify: (message: string, error?: boolean) => void };
const blank = (order: number): ServiceRecord => ({ id: "", slug: "", title: "", subtitle: "", description: "", fullDescription: "", image: "", icon: "book", color: "blue", features: [], sections: [], seoTitle: "", seoDescription: "", displayOrder: order, active: false, version: 1, aliases: [] });

export default function AdminServices({ initialId, notify }: Props) {
  const router = useRouter();
  const { data, error, loading, reload } = useApiResource<ServiceRecord[]>("admin/services", !initialId);
  const [busy, setBusy] = useState(false);
  if (error) return <p role="alert" className="error-message">{error}<button onClick={reload}>تلاش دوباره</button></p>;
  if (loading || !data) return <p role="status">در حال دریافت خدمات…</p>;
  if (initialId) {
    const service = initialId === "new" ? blank(Math.min(10000, Math.max(-1, ...data.map(item => item.displayOrder)) + 1)) : data.find(item => item.id === initialId);
    if (!service) return <section className="panel"><h2>خدمت پیدا نشد</h2><Link href="/admin/services">بازگشت به خدمات</Link></section>;
    return <ServiceEditor key={initialId} initial={service} notify={notify} onSaved={saved => {
      if (!service.id) router.replace("/admin/services/" + saved.id);
      reload();
    }} />;
  }
  async function reorder(index: number, offset: number) {
    if (busy || !data) return;
    const items = [...data]; [items[index], items[index + offset]] = [items[index + offset], items[index]];
    setBusy(true);
    try {
      await api("admin/services/reorder", { method: "POST", body: JSON.stringify({ items: items.map(({ id, version }) => ({ id, version })) }) });
      notify("ترتیب خدمات ذخیره شد."); reload();
    } catch (cause) { notify(cause instanceof Error ? cause.message : "ترتیب ذخیره نشد.", true); }
    finally { setBusy(false); }
  }
  async function toggle(service: ServiceRecord) {
    if (busy) return; setBusy(true);
    try {
      await api("admin/services/" + service.id, { method: "PUT", body: JSON.stringify(servicePayload({ ...service, active: !service.active })) });
      notify(service.active ? "خدمت غیرفعال شد." : "خدمت منتشر شد."); reload();
    } catch (cause) { notify(cause instanceof Error ? cause.message : "ذخیره انجام نشد.", true); }
    finally { setBusy(false); }
  }
  return <section className="services-admin">
    <div className="admin-heading"><div><h2>مدیریت خدمات</h2><p>محتوا، تصویر، ترتیب و وضعیت انتشار خدمات سایت</p></div><Link className="button button-sm" href="/admin/services/new"><Plus size={17} />افزودن خدمت</Link></div>
    <ManagementTable title="خدمات" columns={["نام خدمت","اسلاگ","وضعیت","ترتیب","بخش‌های توضیح","آخرین تغییر"]} refresh={reload} rows={data.map((service,index)=>({id:service.id,status:service.active?"فعال":"غیرفعال",values:[service.title,service.slug,service.active?"فعال":"غیرفعال",service.displayOrder,service.sections.length,service.updatedAt ? new Date(service.updatedAt).toLocaleDateString("fa-IR") : "ثبت نشده"],actions:<>
      <Link href={"/admin/services/"+service.id}>ویرایش</Link>{service.active&&<Link href={"/services/"+service.slug}>مشاهده</Link>}
      <button disabled={busy} onClick={()=>void toggle(service)}>{service.active?"غیرفعال":"فعال"}</button>
      <button aria-label={"حذف "+service.title} disabled={busy} onClick={async()=>{if(!confirm("این خدمت حذف شود؟"))return;setBusy(true);try{await api("admin/services/"+service.id+"?version="+service.version,{method:"DELETE"});reload();}catch(e){notify(e instanceof Error?e.message:"حذف انجام نشد.",true);}finally{setBusy(false);}}}>حذف</button>
      <button disabled={busy||index===0} aria-label="انتقال به بالا" onClick={()=>void reorder(index,-1)}><ArrowUp size={15}/></button>
      <button disabled={busy||index===data.length-1} aria-label="انتقال به پایین" onClick={()=>void reorder(index,1)}><ArrowDown size={15}/></button></>}))}/>
  </section>;
}

function ServiceEditor({ initial, notify, onSaved }: { initial: ServiceRecord; notify: Props["notify"]; onSaved: (saved: ServiceRecord) => void }) {
  const [draft, setDraft] = useState(initial);
  const [featuresText, setFeaturesText] = useState(initial.features.join("\n"));
  const [orderText, setOrderText] = useState(String(initial.displayOrder));
  const snapshot = (item: ServiceRecord, features: string, order: string) => JSON.stringify({ item, features, order });
  const [baseline, setBaseline] = useState(() => snapshot(initial, initial.features.join("\n"), String(initial.displayOrder)));
  const [busy, setBusy] = useState(false), [uploading, setUploading] = useState(false), [error, setError] = useState("");
  const sending = useRef(false);
  const dirty = snapshot(draft, featuresText, orderText) !== baseline;
  useEffect(() => {
    if (!dirty) return;
    const unload = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    const leaving = (event: MouseEvent) => {
      const anchor = (event.target as Element).closest("a");
      if (!anchor || anchor.target === "_blank" || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey) return;
      if (anchor.href !== window.location.href && !window.confirm("تغییرات ذخیره نشده‌اند. از فرم خارج می‌شوید؟")) { event.preventDefault(); event.stopImmediatePropagation(); }
    };
    window.addEventListener("beforeunload", unload); document.addEventListener("click", leaving, true);
    return () => { window.removeEventListener("beforeunload", unload); document.removeEventListener("click", leaving, true); };
  }, [dirty]);
  function change(patch: Partial<ServiceRecord>) { setDraft(current => ({ ...current, ...patch })); }
  async function upload(file: File) {
    if (busy || uploading) return;
    if (file.size > 5 * 1024 * 1024 || !["image/png", "image/jpeg", "image/webp"].includes(file.type)) { setError("تصویر PNG، JPG یا WebP تا ۵ مگابایت انتخاب کنید."); return; }
    setUploading(true); setError("");
    try {
      const response = await sessionFetch("/api/admin/banner-assets", { method: "POST", headers: { "Content-Type": file.type }, body: file });
      const result = await response.json(); if (!response.ok) throw new Error(result.error || "بارگذاری انجام نشد.");
      change({ image: result.url });
    } catch (cause) { setError(cause instanceof Error ? cause.message : "بارگذاری تصویر انجام نشد."); }
    finally { setUploading(false); }
  }
  async function save(event: FormEvent) {
    event.preventDefault(); if (sending.current || uploading) return;
    const order = Number(normalizeDigits(orderText));
    if (!orderText.trim() || !Number.isInteger(order) || order < 0 || order > 10000) { setError("ترتیب نمایش باید عدد صحیح بین صفر و ۱۰٬۰۰۰ باشد."); return; }
    const value = { ...draft, displayOrder: order, features: featuresText.split("\n").map(line => line.trim()).filter(Boolean) };
    sending.current = true; setBusy(true); setError("");
    try {
      const result = await api<{ service: ServiceRecord }>("admin/services" + (draft.id ? "/" + draft.id : ""), { method: draft.id ? "PUT" : "POST", body: JSON.stringify(servicePayload(value)) });
      const saved = result.service;
      setDraft(saved); setFeaturesText(saved.features.join("\n")); setOrderText(String(saved.displayOrder));
      setBaseline(snapshot(saved, saved.features.join("\n"), String(saved.displayOrder)));
      notify("خدمت ذخیره شد."); onSaved(saved);
    } catch (cause) { setError(cause instanceof Error ? cause.message : "خدمت ذخیره نشد."); }
    finally { sending.current = false; setBusy(false); }
  }
  return <div className="service-editor">
    <div className="admin-heading"><div><h2>{draft.id ? "ویرایش خدمت" : "افزودن خدمت"}</h2><p>دکمهٔ عمومی این خدمت «درخواست مشاوره» است.</p></div><Link className="text-link" href="/admin/services">بازگشت به خدمات</Link></div>
    <form className="panel" onSubmit={save} aria-busy={busy || uploading}>
      <fieldset disabled={busy || uploading} className="service-editor-fields"><legend>اطلاعات خدمت</legend>
        <div className="form-grid">
          <label className="field">عنوان خدمت<input name="title" required minLength={2} maxLength={120} value={draft.title} onChange={e => change({ title: e.target.value })} /></label>
          <label className="field">نشانی خدمت<input name="slug" dir="ltr" required maxLength={100} pattern="[a-z0-9]+(-[a-z0-9]+)*" value={draft.slug} onChange={e => change({ slug: e.target.value })} /><small>حروف کوچک انگلیسی، عدد و خط تیره؛ نشانی قبلی به نشانی جدید هدایت می‌شود.</small></label>
          <label className="field full">زیرعنوان<input name="subtitle" maxLength={180} value={draft.subtitle} onChange={e => change({ subtitle: e.target.value })} /></label>
          <label className="field full">توضیح کوتاه<textarea name="description" required minLength={10} maxLength={600} rows={3} value={draft.description} onChange={e => change({ description: e.target.value })} /></label>
          <label className="field full">توضیحات کامل<textarea name="fullDescription" maxLength={12000} rows={8} value={draft.fullDescription} onChange={e => change({ fullDescription: e.target.value })} /></label>
          <label className="field full">ویژگی‌ها<textarea name="features" rows={5} maxLength={1932} value={featuresText} onChange={e => setFeaturesText(e.target.value)} /><small>هر ویژگی در یک خط؛ حداکثر ۱۲ مورد، هر مورد ۱۶۰ کاراکتر.</small></label>
          <label className="field">ترتیب نمایش<input name="displayOrder" inputMode="numeric" required value={orderText} onChange={e => setOrderText(e.target.value)} /></label>
          <label className="check-field"><input type="checkbox" name="active" checked={draft.active} onChange={e => change({ active: e.target.checked })} />انتشار در سایت</label>
          <label className="field">آیکون<select value={draft.icon} onChange={e => change({ icon: e.target.value })}>{[["book", "کتاب"], ["feather", "نشر"], ["palette", "طراحی"], ["layout", "صفحه‌آرایی"], ["pen", "ویرایش"], ["layers", "لایه‌ها"]].map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label>
          <label className="field">رنگ کارت<select value={draft.color} onChange={e => change({ color: e.target.value })}>{[["blue", "آبی"], ["orange", "نارنجی"], ["sage", "سبز"], ["lilac", "بنفش"], ["sand", "کرم"], ["rose", "صورتی"]].map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label>
        </div>
        <section className="service-image-editor"><h3>تصویر خدمت (اختیاری)</h3>
          {draft.image && <Image unoptimized src={draft.image} alt={"پیش‌نمایش " + draft.title} width={360} height={220} />}
          <label className="field">بارگذاری تصویر خدمت<input type="file" accept="image/png,image/jpeg,image/webp" onChange={e => { const file = e.target.files?.[0]; if (file) void upload(file); e.target.value = ""; }} /></label>
          {draft.image && <button type="button" className="text-link" onClick={() => change({ image: "" })}>حذف تصویر و استفاده از تصویرسازی پیش‌فرض</button>}
          <p className="settings-note">PNG / JPG / WebP تا ۵ مگابایت؛ بدون تصویر، تصویرسازی فعلی سایت نمایش داده می‌شود.</p>
        </section>
        <SectionEditor sections={draft.sections} change={sections=>change({sections})}/>
      </fieldset>
      {uploading && <p role="status">در حال بارگذاری تصویر…</p>}
      {error && <p role="alert" className="error-message">{error}</p>}
      <div className="form-actions"><button className="button" disabled={busy || uploading}><Save size={18} />{busy ? "در حال ذخیره…" : "ذخیره خدمت"}</button><Link className="button button-outline" href="/admin/services">انصراف</Link>
        {draft.id && draft.active && !dirty && <Link className="text-link" target="_blank" rel="noopener" href={"/services/" + draft.slug}>مشاهده خدمت</Link>}</div>
    </form>
  </div>;
}
