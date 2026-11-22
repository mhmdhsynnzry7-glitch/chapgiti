"use client";
import { useRouter } from "next/navigation";
import { useRef, useState, type FormEvent } from "react";
import ManagementTable from "./management-table";
import EditorDialog from "./editor-dialog";
import DiscountEditor from "./discount-editor";
import { Plus, Save } from "lucide-react";
import { useApiResource } from "@/hooks/use-api-resource";
import { useUnsavedForm } from "@/hooks/use-unsaved-form";
import { api } from "@/lib/api-client";
import {
  catalogKinds,
  catalogChanged,
  type CatalogItem,
  type CatalogKind,
} from "@/lib/print-catalog";
type Props = {
  initialId?: string;
  notify: (message: string, error?: boolean) => void;
};
const metadataLabels: Record<string, string> = {
  width: "عرض",
  height: "طول",
  unit: "واحد اندازه‌گیری",
  isCustom: "ابعاد سفارشی",
  minWidth: "کمترین عرض",
  maxWidth: "بیشترین عرض",
  minHeight: "کمترین طول",
  maxHeight: "بیشترین طول",
  factor: "ضریب قاعده موجود",
  bw: "صفحات سیاه و سفید",
  color: "صفحات رنگی",
  bwFactor: "ضریب چاپ سیاه و سفید",
  colorFactor: "ضریب چاپ رنگی",
  technology: "فناوری چاپ",
  noPrint: "بدون چاپ متن",
  grams: "گرماژ",
  type: "نوع کاغذ",
  caliper: "ضخامت برگ (میلی‌متر)",
  base: "تعرفه پایه صحافی",
  hard: "جلد سخت",
  extraSpiral: "افزودن هزینه فنر",
  none: "بدون جلد",
  plastic: "جلد طلق",
  laminated: "محاسبه سلفون",
  matte: "سلفون مات",
  rateKey: "تعرفه خدمت",
};
const enumValues: Record<string, string[]> = {
  unit: ["cm"],
  technology: ["digital", "toner", "ink", "mixed", "none"],
  type: ["white", "cream", "coated", "label", "colored"],
  base: ["soft", "hard", "spiral"],
  rateKey: [
    "roundCorners",
    "shrink",
    "goldFoil",
    "silverFoil",
    "spotUV",
    "endpaperBw",
    "endpaperColor",
    "bookmark",
    "coloredEdges",
    "metallicEdges",
    "bellyBand",
    "thickEdges",
    "hardCoverJacket",
    "ribbon",
    "laminatedEndpaper",
  ],
};
export default function AdminPrintCatalog({ initialId, notify }: Props) {
  const { data, error, loading, reload } = useApiResource<{
    items: CatalogItem[];
  }>("admin/print-catalog", !initialId);
  const [editing, setEditing] = useState(initialId || "");
  const router = useRouter();
  const close = () => {setEditing(""); if(initialId)router.replace("/admin/print-settings");};
  const [kind, setKind] = useState<CatalogKind>("bookSizes");
  if (error)
    return (
      <p role="alert" className="error-message">
        {error}
        <button className="button button-outline" onClick={reload}>
          تلاش دوباره
        </button>
      </p>
    );
  if (loading || !data) return <p role="status">در حال دریافت تنظیمات چاپ…</p>;
  let editor = null;
  if (editing && typeof document !== "undefined") {
    const existing = data.items.find((i) => i.id === editing),
      template = data.items.find((i) => i.kind === kind);
    if (editing !== "new" && !existing)
      return <p role="alert">گزینه پیدا نشد.</p>;
    if (!template) return <p role="alert">الگوی این قاعده موجود نیست.</p>;
    const initial = existing ?? {
      ...template,
      id: "",
      code: "",
      label: "",
      isDefault: false,
      active: false,
      version: 1,
      metadata: { ...template.metadata },
      displayOrder:
        Math.max(
          0,
          ...data.items
            .filter((i) => i.kind === kind)
            .map((i) => i.displayOrder),
        ) + 1,
    };
    editor = <EditorDialog title={existing ? "ویرایش گزینه" : "افزودن گزینه"} close={close}><CatalogEditor
        key={editing + ":" + kind}
        initial={initial}
        notify={notify}
        onSaved={()=>{close();reload();}}
        close={close}
        onKindChange={setKind}
      /></EditorDialog>;
  }
  return (
    <section>
      <div className="admin-heading">
        <div>
          <h2>تنظیمات چاپ</h2>
          <p>
            کدهای ثابت و قواعد معتبر؛ تغییرات برای محاسبات آینده اعمال می‌شوند.
          </p>
        </div>
        <button type="button" className="button button-sm" onClick={()=>setEditing("new")}>
          <Plus size={17} />
          افزودن گزینه
        </button>
      </div>
      <div className="catalog-tabs" role="group" aria-label="گروه تنظیمات چاپ">
        {Object.entries(catalogKinds).map(([id, label]) => (
          <button
            className={kind === id ? "selected" : ""}
            aria-pressed={kind === id}
            key={id}
            onClick={() => setKind(id as CatalogKind)}
          >
            {label}
          </button>
        ))}
      </div>
      <ManagementTable key={kind} title={catalogKinds[kind]} columns={["نام","کد داخلی","مشخصات","وضعیت","پیش‌فرض","ترتیب"]} refresh={reload} rows={data.items.filter(i=>i.kind===kind).map(i=>({
        id:i.id,status:i.active?"فعال":"غیرفعال",values:[i.label,i.code,Object.entries(i.metadata).filter(([k])=>["width","height","grams","type","technology"].includes(k)).map(([k,v])=>metadataLabels[k]+" "+String(v)).join("، ")||"—",i.active?"فعال":"غیرفعال",i.isDefault?"بله":"—",i.displayOrder],
        actions:<><button onClick={()=>setEditing(i.id)}>ویرایش</button><button disabled={i.isDefault} onClick={async()=>{if(!window.confirm("این گزینه حذف شود؟"))return;try{await api("admin/print-catalog/"+i.id+"?version="+i.version,{method:"DELETE"});catalogChanged();reload();}catch(e){notify(e instanceof Error?e.message:"حذف انجام نشد.",true);}}}>حذف</button></>
      }))}/><DiscountEditor/>{editor}
    </section>
  );
}
function CatalogEditor({
  initial,
  notify,
  onSaved,
  onKindChange,
  close,
}: {
  initial: CatalogItem;
  notify: Props["notify"];
  onSaved: () => void;
  close: () => void;
  onKindChange: (kind: CatalogKind) => void;
}) {
  const [draft, setDraft] = useState(initial),
    [baseline, setBaseline] = useState(JSON.stringify(initial)),
    [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const saving = useRef(false);
  useUnsavedForm(JSON.stringify(draft) !== baseline);
  const change = (patch: Partial<CatalogItem>) =>
    setDraft((current) => ({ ...current, ...patch }));
  async function save(event: FormEvent) {
    event.preventDefault();
    if (saving.current) return;
    saving.current = true;
    setBusy(true);
    setError("");
    try {
      const response = await api<{ id: string }>(
        "admin/print-catalog" + (draft.id ? "/" + draft.id : ""),
        { method: draft.id ? "PUT" : "POST", body: JSON.stringify(draft) },
      );
      const { items } = await api<{ items: CatalogItem[] }>(
          "admin/print-catalog",
        ),
        saved = items.find((i) => i.id === response.id)!;
      setDraft(saved);
      setBaseline(JSON.stringify(saved));
      catalogChanged();
      notify("تنظیمات چاپ ذخیره شد.");
      onSaved();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "ذخیره انجام نشد.");
    } finally {
      saving.current = false;
      setBusy(false);
    }
  }
  return (
    <section className="catalog-editor">
      <div className="admin-heading">
        <div>
          <h2>{draft.id ? "ویرایش گزینه چاپ" : "افزودن گزینه چاپ"}</h2>
          <p>{catalogKinds[draft.kind]}</p>
        </div>
        <button type="button" className="text-link" onClick={close}>انصراف</button>
      </div>
      <form className="panel" onSubmit={save} aria-busy={busy}>
        <fieldset disabled={busy} className="catalog-editor-fields">
          <legend>مشخصات گزینه</legend>
          <div className="form-grid">
            <label className="field">
              گروه
              <select
                disabled={!!draft.id}
                value={draft.kind}
                onChange={(e) => {
                  if (
                    JSON.stringify(draft) === baseline ||
                    window.confirm(
                      "تغییرات ذخیره نشده‌اند. گروه دیگری انتخاب می‌کنید؟",
                    )
                  )
                    onKindChange(e.target.value as CatalogKind);
                }}
              >
                {Object.entries(catalogKinds).map(([key, label]) => (
                  <option key={key} value={key}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              کد ثابت
              <input
                aria-label="کد ثابت"
                aria-describedby="catalog-code-help"
                name="code"
                dir="ltr"
                required
                maxLength={80}
                pattern="[a-zA-Z0-9][a-zA-Z0-9_-]*"
                readOnly={!!draft.id}
                value={draft.code}
                onChange={(e) => change({ code: e.target.value })}
              />
              <small id="catalog-code-help">پس از ایجاد قابل تغییر نیست.</small>
            </label>
            <label className="field full">
              نام نمایشی فارسی
              <input
                name="label"
                required
                minLength={2}
                maxLength={160}
                value={draft.label}
                onChange={(e) => change({ label: e.target.value })}
              />
            </label>
            <label className="field">
              ترتیب نمایش
              <input
                name="displayOrder"
                type="number"
                min={0}
                max={10000}
                required
                value={draft.displayOrder}
                onChange={(e) =>
                  change({ displayOrder: Number(e.target.value) })
                }
              />
            </label>
            <label className="field">
              قاعده قیمت‌گذاری
              <input readOnly dir="ltr" value={draft.pricingRule} />
              <small>فقط قواعد پشتیبانی‌شده قابل محاسبه‌اند.</small>
            </label>
            <label className="check-field">
              <input
                type="checkbox"
                checked={draft.active}
                onChange={(e) => change({ active: e.target.checked })}
              />
              فعال
            </label>
            <label className="check-field">
              <input
                type="checkbox"
                checked={draft.isDefault}
                disabled={!draft.active}
                onChange={(e) => change({ isDefault: e.target.checked })}
              />
              گزینه پیش‌فرض
            </label>
          </div>
          <h3>ابعاد و سازگاری قیمت‌گذاری</h3>
          <div className="form-grid">
            {Object.entries(draft.metadata)
              .filter(([key]) => key !== "label")
              .map(([key, value]) => (
                <label
                  className={
                    typeof value === "boolean" ? "check-field" : "field"
                  }
                  key={key}
                >
                  {typeof value === "boolean" ? (
                    <>
                      <input
                        type="checkbox"
                        checked={value}
                        onChange={(e) =>
                          change({
                            metadata: {
                              ...draft.metadata,
                              [key]: e.target.checked,
                            },
                          })
                        }
                      />
                      {metadataLabels[key] ?? key}
                    </>
                  ) : (
                    <>
                      {metadataLabels[key] ?? key}
                      {enumValues[key] ? (
                        <select
                          value={String(value)}
                          onChange={(e) =>
                            change({
                              metadata: {
                                ...draft.metadata,
                                [key]: e.target.value,
                              },
                            })
                          }
                        >
                          {enumValues[key].map((v) => (
                            <option key={v} value={v}>
                              {v}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <input
                          type="number"
                          step="any"
                          required
                          value={Number(value)}
                          onChange={(e) =>
                            change({
                              metadata: {
                                ...draft.metadata,
                                [key]: Number(e.target.value),
                              },
                            })
                          }
                        />
                      )}
                    </>
                  )}
                </label>
              ))}
          </div>
          <p className="settings-note">
            واحد ابعاد سانتی‌متر است. گزینه‌های قبلی حذف نمی‌شوند؛ برای توقف
            استفاده، گزینه دیگری را پیش‌فرض و سپس این گزینه را غیرفعال کنید.
          </p>
        </fieldset>
        {error && (
          <p role="alert" className="error-message">
            {error}
          </p>
        )}
        <div className="form-actions">
          <button className="button" disabled={busy}>
            <Save size={17} />
            {busy ? "در حال ذخیره…" : "ذخیره گزینه"}
          </button>
          <button type="button" className="button button-outline" onClick={close}>انصراف</button>
        </div>
      </form>
    </section>
  );
}
