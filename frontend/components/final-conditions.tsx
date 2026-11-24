"use client";
import { useRef, useState } from "react";
import { api } from "@/lib/api-client";
import { createRequestId } from "@/lib/request-id";
import { formatToman } from "@/lib/money";
import type { OrderFile } from "@/lib/api-contracts";
export type Conditions = {
  id: string;
  fileId: string;
  delivery: string;
  scope: string;
  comment: string;
  method?: string;
  address?: string;
  estimate?: string;
  revision: number;
  acceptedAt?: string;
  supersededAt?: string;
};
export default function FinalConditions({
  admin = false,
  id,
  version,
  terms,
  amount,
  fileId,
  files,
  reviewedFileId,
  editable,
  refresh,
}: {
  admin?: boolean;
  id: string;
  version: number;
  terms?: Conditions | null;
  amount: number | null;
  fileId?: string | null;
  files: OrderFile[];
  reviewedFileId?: string;
  editable: boolean;
  refresh: () => void;
}) {
  const [addressNeeded, setAddressNeeded] = useState(!!terms?.address);
  const [postedVersion, setPostedVersion] = useState<number>();
  const [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    sending = useRef(false),
    keys = useRef(new Map<string, string>());
  async function save(path: string, body: Record<string, unknown>) {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    setError("");
    const payload = { ...body, expectedVersion: version },
      signature = path + JSON.stringify(payload);
    if (!keys.current.has(signature))
      keys.current.set(signature, createRequestId());
    try {
      await api((admin ? "admin" : "customer") + "/orders/" + id + "/" + path, {
        method: "POST",
        body: JSON.stringify({
          ...payload,
          idempotencyKey: keys.current.get(signature),
        }),
      });
      setPostedVersion(version);
      refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "ثبت انجام نشد.");
    } finally {
      sending.current = false;
      setBusy(false);
    }
  }
  const file = files.find((f) => f.id === (reviewedFileId ?? fileId)),
    current = terms && !terms.supersededAt && terms.fileId === fileId;
  return (
    <section className="panel delivery-conditions">
      <h3>شرایط نهایی چاپ و تحویل</h3>
      {amount != null && <p>قیمت پذیرفته‌شده · {formatToman(amount)}</p>}
      <p>
        {fileId ? "فایل تأییدشده" : "فایل برای بازبینی"} ·{" "}
        {file ? (
          <a
            href={
              "/api/" +
              (admin ? "admin" : "customer") +
              "/files/" +
              file.id +
              "?preview=true"
            }
          >
            {file.name}{file.fileVersion ? " · نسخهٔ "+file.fileVersion.toLocaleString("fa-IR") : ""}
          </a>
        ) : (
          "در انتظار بررسی"
        )}
      </p>
      {current && (
        <dl>
          <dt>شرایط تحویل</dt>
          <dd>{terms.delivery}</dd>
          {terms.method && <><dt>روش تحویل</dt><dd>{terms.method}</dd></>}
          {terms.address && <><dt>نشانی تحویل</dt><dd>{terms.address}</dd></>}
          {terms.estimate && <><dt>زمان تقریبی</dt><dd>{terms.estimate}</dd></>}
          {terms.scope && <><dt>محدوده خدمات</dt><dd>{terms.scope}</dd></>}
          {terms.comment && (
            <>
              <dt>توضیحات چاپخانه</dt>
              <dd>{terms.comment}</dd>
            </>
          )}
          <dt>نسخه {terms.revision}</dt>
          <dd>{terms.acceptedAt ? "تأیید مشتری" : "در انتظار تأیید مشتری"}</dd>
        </dl>
      )}
      {admin && editable && file && postedVersion !== version && (
        <details key={terms?.id ?? "new"} onToggle={event=>{if (event.currentTarget.open) setAddressNeeded(!!terms?.address);}}>
          <summary>{current ? "ویرایش / اصلاح شرایط نهایی" : "تنظیم شرایط نهایی"}</summary>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              void save("delivery-terms", {
                reviewedFileId: file.id,
                ...(current ? {revisionOf:terms.id} : {}),
                delivery: f.get("delivery"),
                method: f.get("method"), addressNeeded, address: addressNeeded ? f.get("address") : "", estimate: f.get("estimate"),
                scope: f.get("scope"),
                comment: f.get("comment"),
              });
            }}
          >
            <label className="field">روش تحویل توافق‌شده<input name="method" defaultValue={terms?.method ?? ""} maxLength={160}/></label>
            <label className="check-field"><input type="checkbox" checked={addressNeeded} onChange={e=>setAddressNeeded(e.target.checked)}/>این تحویل به نشانی نیاز دارد</label>
            {addressNeeded && <label className="field">نشانی تحویل<input name="address" defaultValue={terms?.address ?? ""} required minLength={5} maxLength={600}/></label>}
            <label className="field">زمان تقریبی توافق‌شده<input name="estimate" defaultValue={terms?.estimate ?? ""} maxLength={160}/></label>
            <label className="field">
              شرایط تحویل
              <textarea
                aria-label="شرایط تحویل" name="delivery" defaultValue={terms?.delivery ?? ""}
                required
                minLength={2}
                maxLength={2000}
              />
            </label>
            <label className="field">
              محدوده خدمات غیرمالی
              <textarea aria-label="محدوده خدمات غیرمالی" name="scope" defaultValue={terms?.scope ?? ""} maxLength={2000} />
            </label>
            <label className="field">
              توضیح برای مشتری
              <textarea aria-label="توضیح برای مشتری" name="comment" defaultValue={terms?.comment ?? ""} maxLength={2000} />
            </label>
            <button className="button" disabled={busy}>{busy ? "در حال ارسال…" : current ? "ثبت اصلاح و درخواست تأیید دوباره" : "ارسال شرایط نهایی برای تأیید مشتری"}</button>
          </form>
        </details>
      )}
      {!admin && editable && current && !terms.acceptedAt && (
        <><button
          className="button"
          disabled={busy}
          onClick={() =>
            void save("delivery-terms/accept", { termsId: terms.id })
          }
        >
          تأیید نهایی و ثبت توافق
        </button>
        <details><summary>درخواست اصلاح شرایط</summary>
          <form onSubmit={event => {event.preventDefault(); void save("delivery-terms/request-amendment", {termsId:terms.id, reason:new FormData(event.currentTarget).get("reason")});}}>
            <label className="field">شرح اصلاح شرایط<textarea name="reason" required minLength={2} maxLength={900}/></label>
            <button className="button button-outline" disabled={busy}>ارسال درخواست اصلاح شرایط</button>
          </form>
        </details></>
      )}
      {error && <p role="alert">{error}</p>}
    </section>
  );
}
