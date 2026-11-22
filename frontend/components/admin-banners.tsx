"use client";
import { useRef, useState, type FormEvent } from "react";
import Image from "next/image";
import { Plus, Save, Trash2, ArrowUp, ArrowDown, Upload, ImageIcon, Pencil, X } from "lucide-react";
import { createRequestId } from "@/lib/request-id";
import { useApiResource } from "@/hooks/use-api-resource";
import { sessionFetch } from "@/lib/session-client";
import { Dialog, DialogContent, DialogTitle, DialogDescription } from "./ui/dialog";
import type { Banner } from "@/lib/banners";

type Props = { notify: (text: string, error?: boolean) => void; mode?: "banners" | "portfolio" };
type Editor = { draft: Banner; original: string; creating: boolean };

export default function AdminBanners({ notify, mode = "banners" }: Props) {
  const { data, error, loading, reload } = useApiResource<Banner[]>("admin/" + mode);
  if (error) return <div className="error-message" role="alert">{error}<button className="button button-sm" onClick={reload}>تلاش دوباره</button></div>;
  if (loading || !data) return <p role="status">در حال دریافت اطلاعات…</p>;
  return <BannerEditor key={mode} initial={data} mode={mode} notify={notify} />;
}

function BannerEditor({ initial, notify, mode = "banners" }: Props & { initial: Banner[] }) {
  const portfolio = mode === "portfolio", itemName = portfolio ? "نمونه‌کار" : "بنر", limit = portfolio ? 30 : 12;
  const [banners, setBanners] = useState(initial), [editor, setEditor] = useState<Editor | null>(null);
  const [busy, setBusy] = useState(false), [uploading, setUploading] = useState(false), [error, setError] = useState("");
  const sending = useRef(false), opener = useRef<HTMLButtonElement | null>(null);
  const draft = editor?.draft;
  const visible = portfolio ? banners : banners.filter(banner => banner.kind !== "portfolio");
  function open(button: HTMLButtonElement, banner?: Banner) {
    opener.current = button; setError("");
    const value: Banner = banner ? { ...banner } : { id: createRequestId(), title: "", subtitle: "", eyebrow: "", image: "", link: "", label: "", theme: "navy", kind: portfolio ? "portfolio" : "promotion", active: true, demo: portfolio };
    if (portfolio && !banner) { value.link = "/portfolio/" + encodeURIComponent(value.id); value.label = "مشاهده نمونه‌کار"; }
    setEditor({ draft: value, original: JSON.stringify(value), creating: !banner });
  }
  function close() {
    if (busy || uploading) return;
    if (editor && JSON.stringify(editor.draft) !== editor.original && !window.confirm("تغییرات ذخیره نشده‌اند. از فرم خارج می‌شوید؟")) return;
    setEditor(null); setError("");
  }
  function change(patch: Partial<Banner>) {
    setEditor(current => current ? { ...current, draft: { ...current.draft, ...patch } } : null);
  }
  async function persist(next: Banner[]) {
    const response = await sessionFetch("/api/admin/" + mode, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(next) });
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || "ذخیره انجام نشد.");
    setBanners(next);
  }
  async function save(event: FormEvent) {
    event.preventDefault();
    if (sending.current || !editor) return;
    const value = { ...editor.draft, title: editor.draft.title.trim(), label: editor.draft.label.trim() };
    const internal = (path: string) => path.startsWith("/") && !path.startsWith("//") && !/[\\\r\n]/.test(path);
    if (value.title.length < 3 || ((value.label !== "" || value.link !== "") && value.label.length < 2)) {
      setError("عنوان و در صورت فعال‌بودن دکمه، متن آن را کامل کنید."); return;
    }
    if (!internal(value.image)) {
      setError("برای این مورد یک تصویر معتبر بارگذاری کنید."); return;
    }
    if ((value.label !== "" || value.link !== "") && !internal(value.link)) {
      setError("پیوند دکمه باید به یکی از صفحه‌های همین سایت اشاره کند."); return;
    }
    sending.current = true; setBusy(true); setError("");
    try {
      await persist(editor.creating ? [...banners, value] : banners.map(row => row.id === value.id ? value : row));
      setEditor(null); notify(itemName + " ذخیره شد.");
    } catch (cause) { setError(cause instanceof Error ? cause.message : "ذخیره انجام نشد."); }
    finally { sending.current = false; setBusy(false); }
  }
  async function updateList(next: Banner[], message: string) {
    if (sending.current) return;
    sending.current = true; setBusy(true);
    try { await persist(next); notify(message); }
    catch (cause) { notify(cause instanceof Error ? cause.message : "ذخیره انجام نشد.", true); }
    finally { sending.current = false; setBusy(false); }
  }
  function move(index: number, delta: number) {
    const next = [...visible]; [next[index], next[index + delta]] = [next[index + delta], next[index]];
    let cursor = 0;
    void updateList(banners.map(item => !portfolio && item.kind === "portfolio" ? item : next[cursor++]), "ترتیب ذخیره شد.");
  }
  async function upload(file: File) {
    if (uploading || busy) return;
    if (file.size > 5 * 1024 * 1024 || !["image/png", "image/jpeg", "image/webp"].includes(file.type)) { setError("تصویر PNG، JPG یا WebP تا حجم ۵ مگابایت انتخاب کنید."); return; }
    setUploading(true); setError("");
    try {
      const response = await sessionFetch("/api/admin/banner-assets", { method: "POST", headers: { "Content-Type": file.type }, body: file });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || "تصویر بارگذاری نشد.");
      change({ image: data.url });
    } catch (cause) { setError(cause instanceof Error ? cause.message : "تصویر بارگذاری نشد."); }
    finally { setUploading(false); }
  }
  return <div className="banner-editor">
    <div className="admin-heading"><div><h2>{portfolio ? "مدیریت نمونه‌کارها" : "بنرهای تبلیغات"}</h2><p>افزودن و ویرایش {itemName} در فرم جداگانه؛ ترتیب و وضعیت نمایش از فهرست مدیریت می‌شود.</p></div>
      <button className="button button-sm" disabled={busy || banners.length >= limit} onClick={e => open(e.currentTarget)}><Plus size={17} />افزودن {itemName}</button></div>
    <div className="content-records">{visible.map((banner, index) => <article className="panel content-record" key={banner.id}>
      <div className={"content-record-image " + banner.theme}><Image unoptimized width={130} height={100} src={banner.image} alt={banner.title} /></div>
      <div className="content-record-text"><h3>{banner.title}</h3><p>{banner.subtitle}</p><small>{banner.active ? "در حال نمایش" : "غیرفعال"} · ترتیب {(index + 1).toLocaleString("fa-IR")}</small></div>
      <div className="content-record-actions">
        <button className="button button-sm button-outline" disabled={busy} onClick={e => open(e.currentTarget, banner)} aria-label={"ویرایش " + banner.title}><Pencil size={16} />ویرایش</button>
        <button className="icon-button" aria-label={"انتقال به بالا " + banner.title} disabled={busy || index === 0} onClick={() => move(index, -1)}><ArrowUp size={17} /></button>
        <button className="icon-button" aria-label={"انتقال به پایین " + banner.title} disabled={busy || index === visible.length - 1} onClick={() => move(index, 1)}><ArrowDown size={17} /></button>
        <button className="icon-button danger" aria-label={"حذف " + banner.title} disabled={busy} onClick={() => { if (window.confirm("این " + itemName + " حذف شود؟")) void updateList(banners.filter(row => row.id !== banner.id), itemName + " حذف شد."); }}><Trash2 size={17} /></button>
      </div>
    </article>)}</div>
    {visible.length === 0 && <div className="panel empty-state"><ImageIcon /><p>برای شروع یک {itemName} اضافه کنید.</p></div>}
    <Dialog open={!!editor} onOpenChange={value => { if (!value) close(); }}>
      <DialogContent dir="rtl" showCloseButton={false} className="giti-editor-dialog" onCloseAutoFocus={e => { e.preventDefault(); opener.current?.focus(); }}>
        <div className="dialog-title-row"><DialogTitle>{editor?.creating ? "افزودن " : "ویرایش "}{itemName}</DialogTitle><button type="button" className="icon-button" aria-label="بستن فرم" disabled={busy || uploading} onClick={close}><X size={20} /></button></div>
        <DialogDescription>اطلاعات و تصویر {itemName} را بررسی و ذخیره کنید.</DialogDescription>
        {draft && <form onSubmit={save} aria-busy={busy || uploading}>
          <BannerFields draft={draft} portfolio={portfolio} change={change} uploading={uploading} disabled={busy} upload={upload} />
          {error && <p className="error-message" role="alert">{error}</p>}
          <div className="dialog-actions"><button className="button" disabled={busy || uploading}><Save size={18} />{busy ? "در حال ذخیره…" : "ذخیره " + itemName}</button><button type="button" className="button button-outline" disabled={busy || uploading} onClick={close}>انصراف</button></div>
        </form>}
      </DialogContent>
    </Dialog>
  </div>;
}

function BannerFields({ draft, portfolio, change, uploading, disabled, upload }: {
  draft: Banner; portfolio: boolean; change: (patch: Partial<Banner>) => void; uploading: boolean;
  disabled: boolean; upload: (file: File) => Promise<void>;
}) {
  return <fieldset className="banner-modal-fields" disabled={disabled}>
    <div className={"banner-image-preview " + draft.theme}>{draft.image ? <Image unoptimized width={520} height={240} src={draft.image} alt={"پیش‌نمایش " + draft.title} /> : <ImageIcon size={48} aria-hidden="true" />}
      <label className="button button-sm button-white" role="button" tabIndex={0} aria-disabled={uploading || disabled} onKeyDown={e => { if (!uploading && !disabled && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); e.currentTarget.querySelector("input")?.click(); } }}><Upload size={16} />{uploading ? "در حال بارگذاری…" : "بارگذاری تصویر"}<input type="file" accept="image/png,image/jpeg,image/webp" hidden disabled={uploading} onChange={e => { const file = e.target.files?.[0]; if (file) void upload(file); e.target.value = ""; }} /></label><small>PNG / JPG / WebP · حداکثر ۵ مگابایت</small></div>
    <div className="form-grid">
      <label className="field">عنوان {portfolio ? "نمونه‌کار" : "بنر"}<textarea name="title" required minLength={3} maxLength={120} value={draft.title} onChange={e => change({ title: e.target.value })} /></label>
      <label className="field">متن بالای عنوان<input maxLength={80} value={draft.eyebrow} onChange={e => change({ eyebrow: e.target.value })} /></label>
      <label className="field full">توضیح<textarea maxLength={350} value={draft.subtitle} onChange={e => change({ subtitle: e.target.value })} /></label>
      {!portfolio && <label className="check-field full"><input type="checkbox" checked={!!(draft.label || draft.link)} onChange={e => change(e.target.checked ? {label:"مشاهده خدمات",link:"/services"} : {label:"",link:""})} />نمایش دکمه در بنر</label>}
      {!portfolio && !!(draft.label || draft.link) && <label className="field">متن دکمه<input required minLength={2} maxLength={60} value={draft.label} onChange={e => change({ label: e.target.value })} /></label>}
      {!portfolio && !!(draft.label || draft.link) && <label className="field">لینک دکمه<input required dir="ltr" maxLength={1000} value={draft.link} onChange={e => change({ link: e.target.value })} /></label>}
      <label className="field">رنگ زمینه<select value={draft.theme} onChange={e => change({ theme: e.target.value as Banner["theme"] })}><option value="navy">سرمه‌ای</option><option value="orange">نارنجی</option><option value="sage">سبز</option></select></label>
      <label className="check-field"><input type="checkbox" checked={draft.active} onChange={e => change({ active: e.target.checked })} />نمایش در سایت</label>
      {portfolio && <label className="check-field"><input type="checkbox" checked={!!draft.demo} onChange={e => change({ demo: e.target.checked })} />نمونه طراحی نمایشی</label>}
    </div>
  </fieldset>;
}
