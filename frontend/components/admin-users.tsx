"use client";
import ManagementTable, { DateSort, PageSizeControl } from "./management-table";
import Pagination from "./pagination";
import { useRef, useState, type FormEvent } from "react";
import NewPasswordFields from "./new-password-fields";
import { passwordError } from "@/lib/password-policy";
import { Plus, Search } from "lucide-react";
import { useApiResource } from "@/hooks/use-api-resource";
import { api } from "@/lib/api-client";
import { date, normalizeDigits } from "@/lib/pricing";
import { Dialog, DialogContent, DialogTitle, DialogDescription } from "./ui/dialog";

type User = { id: string; firstName: string; lastName: string; phone: string; createdAt: string; phoneVerified: boolean; roles: string[]; lastLoginAt: number | null; loginCount: number; activeSessions: number; orderCount: number };
type UsersPage = { items: User[]; total: number; page: number; pageSize: number };
type Draft = { firstName: string; lastName: string; phone: string; password: string; confirmation: string };
const empty: Draft = { firstName: "", lastName: "", phone: "", password: "", confirmation: "" };

export default function AdminUsers({ notify }: { userId: string; notify: (text: string, error?: boolean) => void }) {
  const [oldestFirst,setOldestFirst]=useState(false);
  const [pageSize,setPageSize]=useState(20);
  const [query, setQuery] = useState(""), [search, setSearch] = useState(""), [page, setPage] = useState(1);
  const params = new URLSearchParams({ q: search, page: String(page), pageSize: String(pageSize), oldestFirst:String(oldestFirst) });
  const { data, loading, error, reload } = useApiResource<UsersPage>("admin/users?" + params);
  const [selected, setSelected] = useState<User | null>(null);
  const [open, setOpen] = useState(false), [draft, setDraft] = useState<Draft>(empty);
  const [saving, setSaving] = useState(false), [saveError, setSaveError] = useState("");
  const sending = useRef(false), addButton = useRef<HTMLButtonElement>(null);
  function close() {
    if (saving) return;
    if (Object.values(draft).some(Boolean) && !window.confirm("اطلاعات واردشده ذخیره نشده است. از فرم خارج می‌شوید؟")) return;
    setOpen(false); setDraft(empty); setSaveError("");
  }
  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (sending.current) return; setSaveError("");
    if (passwordError(draft.password, draft.confirmation)) { setSaveError(passwordError(draft.password, draft.confirmation)); return; }
    if (draft.firstName.trim().length < 2 || draft.lastName.trim().length < 2) { setSaveError("نام و نام خانوادگی باید حداقل دو نویسه باشند."); return; }
    sending.current = true; setSaving(true);
    try {
      await api("admin/users", { method: "POST", body: JSON.stringify({ ...draft, firstName: draft.firstName.trim(), lastName: draft.lastName.trim() }) });
      setOpen(false); setDraft(empty); reload(); notify("کاربر ساخته شد. تأیید شماره باید توسط خود کاربر انجام شود.");
    } catch (cause) { setSaveError(cause instanceof Error ? cause.message : "کاربر ساخته نشد."); }
    finally { sending.current = false; setSaving(false); }
  }
  return <section>
    <div className="admin-heading"><div><h2>مشتریان</h2><p>مشاهده و ایجاد حساب مشتری؛ اطلاعات شخصی کاربران قابل ویرایش نیست.</p></div>
      <button ref={addButton} className="button button-sm" onClick={() => { setDraft(empty); setSaveError(""); setOpen(true); }}><Plus size={17} />ایجاد کاربر</button></div>
    <form className="user-filters" onSubmit={e => { e.preventDefault(); setSearch(query); setPage(1); }}>
      <DateSort oldest={oldestFirst} change={v=>{setOldestFirst(v);setPage(1);}}/>
          <label className="field">جست‌وجوی نام یا موبایل<input value={query} maxLength={100} onChange={e => setQuery(e.target.value)} /></label>
      <button className="button button-outline"><Search size={17} />جست‌وجو</button>
    </form>
    {loading && <p role="status">در حال دریافت کاربران…</p>}
    {error && <div className="error-message" role="alert">{error}<button className="button button-sm" onClick={reload}>تلاش دوباره</button></div>}
    <p className="settings-note">سابقهٔ ورود از نشست‌های ذخیره‌شده است؛ نبود سابقه به معنی اثباتِ هرگز وارد نشدن نیست. نشست معتبر نشان‌دهندهٔ آنلاین بودن نیست.</p>
    {data && <><ManagementTable remote title="مشتریان" refresh={reload} busy={loading} columns={["نام مشتری","موبایل","ایجاد حساب","آخرین ورود","سفارش‌ها"]} rows={data.items.map(user=>({id:user.id,values:[(user.firstName+" "+user.lastName).trim()||user.phone,user.phone,date(user.createdAt),user.lastLoginAt!=null?new Date(user.lastLoginAt).toLocaleString("fa-IR",{timeZone:"Asia/Tehran"}):"—",user.orderCount],actions:<button onClick={()=>setSelected(user)}>مشاهده جزئیات</button>}))}/>
      <PageSizeControl value={pageSize} change={n=>{setPageSize(n);setPage(1);}}/>
          <Pagination className="user-pagination" page={page} pageSize={data.pageSize} total={data.total} noun="مشتری" loading={loading} onPageChange={setPage} /></>}
    <Dialog open={!!selected} onOpenChange={value => { if (!value) setSelected(null); }}><DialogContent dir="rtl" className="giti-editor-dialog"><DialogTitle>جزئیات مشتری</DialogTitle><DialogDescription>اطلاعات حساب فقط توسط صاحب حساب قابل ویرایش است.</DialogDescription>{selected && <dl className="order-information"><div><dt>نام</dt><dd>{(selected.firstName + " " + selected.lastName).trim() || selected.phone}</dd></div><div><dt>موبایل</dt><dd><bdi>{selected.phone}</bdi></dd></div><div><dt>تأیید شماره</dt><dd>{selected.phoneVerified ? "تأییدشده" : "تأییدنشده"}</dd></div><div><dt>ورودهای ثبت‌شده</dt><dd>{selected.loginCount.toLocaleString("fa-IR")}</dd></div><div><dt>نشست معتبر</dt><dd>{selected.activeSessions.toLocaleString("fa-IR")}</dd></div><div><dt>سفارش‌ها</dt><dd>{selected.orderCount.toLocaleString("fa-IR")}</dd></div></dl>}</DialogContent></Dialog>
    <Dialog open={open} onOpenChange={value => { if (!value) close(); }}>
      <DialogContent dir="rtl" showCloseButton={false} className="giti-editor-dialog" onCloseAutoFocus={e => { e.preventDefault(); addButton.current?.focus(); }}>
        <DialogTitle>ایجاد کاربر</DialogTitle><DialogDescription>رمز اولیهٔ اختصاصی تعیین کنید و آن را از مسیر امن به صاحب حساب برسانید. شماره موبایل تا تأیید خود کاربر، تأییدنشده می‌ماند.</DialogDescription>
        <form onSubmit={create} aria-busy={saving}>
          <div className="form-grid">{(["firstName", "lastName", "phone"] as const).map(key => <label className="field" key={key}>{({ firstName: "نام", lastName: "نام خانوادگی", phone: "شماره موبایل", password: "رمز اولیه", confirmation: "تکرار رمز اولیه" })[key]}
            <input name={key} value={draft[key]} required disabled={saving} type={key === "phone" ? "tel" : "text"} autoComplete="off" minLength={2} maxLength={key === "phone" ? 11 : 80} pattern={key === "phone" ? "09[0-9]{9}" : undefined} dir={key === "phone" ? "ltr" : undefined} onChange={e => setDraft({ ...draft, [key]: key === "phone" ? normalizeDigits(e.target.value) : e.target.value })} /></label>)}</div>
          <NewPasswordFields password={draft.password} confirmation={draft.confirmation} onPassword={password => setDraft(previous => ({ ...previous, password }))} onConfirmation={confirmation => setDraft(previous => ({ ...previous, confirmation }))} disabled={saving} />
          {saveError && <p className="error-message" role="alert">{saveError}</p>}
          <div className="dialog-actions"><button className="button" disabled={saving}>{saving ? "در حال ایجاد…" : "ایجاد کاربر"}</button><button type="button" className="button button-outline" disabled={saving} onClick={close}>انصراف</button></div>
        </form>
      </DialogContent>
    </Dialog>
  </section>;
}
