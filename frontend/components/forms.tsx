"use client";
import { useHydrated } from "@/hooks/use-hydrated";
import { formatToman } from "@/lib/money";
import { sessionFetch } from "@/lib/session-client";
import Link from "next/link";
import { useState, useEffect, useRef, type FormEvent } from "react";
import {
  ArrowUpLeft,
  Upload,
  FileCheck,
  CheckCircle2,
  Search,
  Copy,
} from "lucide-react";
import { defaultSpec, money, type PrintSpec } from "@/lib/pricing";
import OrderTrackingDetails from "./order-tracking-details";
import { createRequestId } from "@/lib/request-id";
import { orderServices } from "@/lib/order-requirements";
import BookSpecFields from "./book-spec-fields";
import { detailsFromSpec, withBookDetails } from "@/lib/book-pricing";
export { api } from "@/lib/api-client";
import { api } from "@/lib/api-client";
import type { CustomerProfile } from "@/lib/customer";
import type { OrderReceipt, TrackingResult } from "@/lib/api-contracts";
import PrintRequestLink from "./print-request-link";
import { readRequestSpec, storeRequestSpec, clearRequestSpec } from "@/lib/request-draft";

import { usePrintCatalog, CatalogStatus } from "./print-catalog-provider";
import { catalogInitialSpec, attachCatalogIds } from "@/lib/print-catalog";
import { validatePrintSpec } from "@/lib/print-spec";
export function PrintFields({
  spec,
  setSpec,
}: {
  spec: PrintSpec;
  setSpec: (spec: PrintSpec) => void;
}) {
  return <BookSpecFields spec={spec} onChange={setSpec} order />;
}
export function QuotePanel({
  spec,
  link = true,
}: {
  spec: PrintSpec;
  link?: boolean;
}) {
  const catalog = usePrintCatalog();
  const [quoted, setQuoted] = useState<{key: string; total?: number; unit?:number; error?: string} | null>(null);
  const key = JSON.stringify(spec);
  useEffect(() => {
    const abort = new AbortController();
    const timer = window.setTimeout(async () => {
      try {
        if (!catalog.options || catalog.error) return;
        const payload = attachCatalogIds(validatePrintSpec(JSON.parse(key), catalog.options), catalog.items);
        const value = await api<{total: number; unit:number}>("quote", {method: "POST", body: JSON.stringify(payload), signal: abort.signal});
        if (!abort.signal.aborted) setQuoted({key, total: value.total,unit:value.unit});
      } catch (cause) { if (!abort.signal.aborted) setQuoted({key,error:cause instanceof Error ? cause.message : "دریافت برآورد انجام نشد."}); }
    }, 500);
    return () => { clearTimeout(timer); abort.abort(); };
  }, [catalog.options, catalog.error, catalog.items, key]);
  const valid = quoted?.key === key && quoted.total !== undefined;
  return (
    <aside className="panel quote-panel" aria-live="polite">
      <div className="panel-title">
        <h2>برآورد سفارش شما</h2>
        <span>تومان</span>
      </div>
      <div className="quote-total">
        <span>برآورد کل · {money(spec.quantity)} جلد</span>
        <strong>{valid ? formatToman(quoted!.total!) : "—"}</strong>
      </div>
      <dl className="order-spec-summary">
        <div><dt>قطع کتاب</dt><dd>{catalog.options?.bookSizes[spec.size]?.label}</dd></div>
        {valid && <div><dt>برآورد هر جلد</dt><dd>{formatToman(quoted!.unit)} برای هر جلد</dd></div>}
        {!!spec.details?.extras.length && <div><dt>خدمات تکمیلی</dt><dd>{spec.details.extras.map(k=>catalog.options?.finishingServices[k]?.label).filter(Boolean).join("، ")}</dd></div>}
      </dl>
      {link && valid && (
        <PrintRequestLink className="button button-full" spec={spec}>
          ارسال درخواست چاپ <ArrowUpLeft size={18} />
        </PrintRequestLink>
      )}
      <p>
        قیمت نهایی پس از بررسی فایل و تأیید مشخصات اعلام می‌شود. هزینه طراحی،
        خدمات نشر و ارسال جداگانه محاسبه می‌شود.
      </p>
      {quoted?.key === key && quoted.error && <p className="notice" role="status">{quoted.error}</p>}
    </aside>
  );
}
export function OrderForm(props: Parameters<typeof OrderFormFields>[0]) {
  const hydrated = useHydrated();
  const catalog = usePrintCatalog();
  if (!hydrated)
    return <p role="status">در حال بازیابی مشخصات محاسبه‌شده…</p>;
  if (!catalog.options) return <CatalogStatus />;
  let saved:PrintSpec | null = null;
  if (props.restoreDraft || (!props.initialSpec && Object.keys(props.initialOverrides ?? {}).length === 0)) {
    try { saved=readRequestSpec(sessionStorage); } catch { /* Storage may be disabled by the browser. */ }
  }
  return (
    <>
      {props.restoreDraft && !saved && (
        <p className="notice">
          مشخصات موقت در دسترس نیست؛ پیش از ارسال، مشخصات زیر را بررسی کنید.
        </p>
      )}
      <OrderFormFields
        {...props}
        initialSpec={catalogInitialSpec(
          catalog.items,
          catalog.options,
          saved ?? props.initialSpec ?? props.initialOverrides,
        )}
      />
    </>
  );
}
function OrderFormFields({
  initialSpec = defaultSpec,
}: {
  initialSpec?: PrintSpec;
  initialOverrides?: Partial<PrintSpec>;
  restoreDraft?: boolean;
}) {
  const catalog = usePrintCatalog(),
    options = catalog.options!;
  const requestKey = useRef<string | null>(null);
  const [spec, setSpec] = useState<PrintSpec>(() =>
    withBookDetails(
      initialSpec,
      detailsFromSpec(initialSpec, options),
      options,
    ),
  );
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<OrderReceipt | null>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [uploaded, setUploaded] = useState<string[]>([]);
  const [uploadError, setUploadError] = useState("");
  const [uploading, setUploading] = useState(false);
  const [copied, setCopied] = useState(false);
  const [service, setService] = useState("book-printing");
  const [title, setTitle] = useState("");
  const fileSelection = useRef(0);
  const [termsAccepted, setTermsAccepted] = useState(false);

  async function upload(order: OrderReceipt, list: File[]) {
    setUploading(true);
    setUploadError("");
    let count = 0;
    for (const file of list) {
      try {
        const res = await sessionFetch("/api/orders/" + order.id + "/files", {
          method: "POST",
          headers: {
            "Content-Type": "application/pdf",
            "x-order-code": order.code,
            "x-file-name": encodeURIComponent(file.name),
          },
          body: file,
        });
        const data: { error?: string } = await res.json();
        if (!res.ok) throw new Error(data.error);
        setUploaded((prev) => [...prev, file.name]);
        count++;
      } catch (e) {
        setUploadError((e as Error).message);
        break;
      }
    }
    setFiles(list.slice(count));
    setUploading(false);
  }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (pending) return;
    setPending(true);
    setError("");
    const f = new FormData(e.currentTarget);
    try {
      if (!files.length)
        throw new Error(
          "برای ثبت این خدمت ابتدا فایل PDF معتبر را انتخاب کنید.",
        );
      const payload = {
        termsAccepted,
        title: f.get("title"),
        notes: f.get("notes") || "",
        service: f.get("service"),
        spec: attachCatalogIds(validatePrintSpec(spec, options), catalog.items),
        idempotencyKey: (requestKey.current ??= createRequestId()),
      };
      const multipart = new FormData();
      if (files[0]) {
        multipart.set("payload", JSON.stringify(payload));
        multipart.set("file", files[0]);
      }
      const data = await api<OrderReceipt>("orders", {
        method: "POST",
        body: files[0] ? multipart : JSON.stringify(payload),
      });
      setResult(data);
      try {
        clearRequestSpec(sessionStorage);
      } catch {}
      if (files[0]) setUploaded([files[0].name]);
      if (files.length > 1) await upload(data, files.slice(1));
      else setFiles([]);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setPending(false);
    }
  }
  if (result)
    return (
      <div className="two-col">
        <section className="panel">
          <span className="service-icon">
            <CheckCircle2 />
          </span>
          <h2>درخواست شما ثبت شد.</h2>
          <p style={{ margin: "12px 0", fontSize: 13 }}>
            درخواست برای بررسی چاپخانه ارسال شد. پیشنهاد قیمت را در حساب خود
            می‌بینید؛ تا تأیید شما چاپ شروع نمی‌شود.
          </p>
          <code className="tracking-code">{result.code}</code>
          <div className="form-actions">
            <button
              className="button button-outline"
              onClick={async () => {
                try {
                  await navigator.clipboard.writeText(result.code);
                  setCopied(true);
                } catch {
                  setCopied(false);
                }
              }}
            >
              <Copy size={16} />
              {copied ? "کپی شد" : "کپی کد پیگیری"}
            </button>
            <Link
              prefetch={false}
              className="button"
              href={"/account/orders/" + result.id}
            >
              پیگیری سفارش
            </Link>
            <Link prefetch={false} className="text-link" href="/account/orders">
              سفارش‌های من
            </Link>
          </div>
          <div className="notice">
            برآورد سفارش: {formatToman(result.estimate)} · در انتظار بررسی
            کارشناس
          </div>
          {uploading && (
            <p role="status">در حال بارگذاری فایل‌ها؛ لطفاً صفحه را نبندید.</p>
          )}
          {uploaded.map((name, i) => (
            <div className="file-row" key={i}>
              <FileCheck size={18} />
              {name}
              <span>بارگذاری شد</span>
            </div>
          ))}
          {uploadError && (
            <div className="error-message" role="alert">
              {uploadError}
              <br />
              سفارش ثبت شده؛ برای ارسال مجدد فایل از دکمه زیر استفاده کنید.
            </div>
          )}
          {!uploading && files.length > 0 && (
            <button
              className="button button-outline"
              onClick={() => upload(result, files)}
            >
              تلاش مجدد برای فایل‌های باقی‌مانده
            </button>
          )}
        </section>
        <aside className="help-card">
          <h3>قدم بعدی چیست؟</h3>
          <p>
            فایل و مشخصات درخواست بررسی می‌شود. برای نهایی‌سازی قیمت و برنامه
            تولید با شما هماهنگ می‌کنیم.
          </p>
          <ul>
            <li>کد پیگیری را ذخیره کنید.</li>
            <li>نیازی به ثبت دوباره سفارش نیست.</li>
            <li>روند سفارش را از صفحه پیگیری ببینید.</li>
          </ul>
        </aside>
      </div>
    );
  return (
    <form onSubmit={submit} className="calculator-layout">
      <div className="panel">
        <div className="panel-title">
          <h2>درخواست چاپ و نشر</h2>
          <span>ثبت سفارش اولیه</span>
        </div>
        <div className="form-section-label">
          <span>۱</span> مشخصات کتاب
        </div>
        <PrintFields spec={spec} setSpec={next=>{
          setSpec(next);
          try {storeRequestSpec(sessionStorage,next);} catch { /* Keep unfinished fields in the form until valid. */ }
        }}/>
        <div className="form-section-label"><span>۲</span> مشخصات درخواست</div>
        <div className="form-grid" style={{ marginBottom: 20 }}>
          <label className="field">
            عنوان کتاب یا پروژه
            <input
              name="title"
              value={title}
              onChange={e=>setTitle(e.target.value)}
              required
              minLength={2}
              maxLength={160}
              placeholder="مثلاً: مجموعه داستان من"
            />
          </label>
          <label className="field">
            خدمت مورد نیاز
            <select
              name="service"
              value={service}
              onChange={(e) => {
                setService(e.target.value);
                requestKey.current = null;
              }}
            >
              {Object.entries(orderServices).map(([id, label]) => (
                <option key={id} value={id}>
                  {label}
                </option>
              ))}
            </select>
          </label>
        </div>
        <hr className="form-divider" />
        <details className="optional-order-notes"><summary>توضیحات سفارش (اختیاری)</summary>
        <div className="form-grid">
          <label className="field full">
            توضیحات و خدمات مورد نیاز
            <textarea
              name="notes"
              maxLength={3000}
              placeholder="اگر طراحی، ویرایش یا خدمات دیگری نیاز دارید، اینجا بنویسید."
            />
          </label>
        </div>
        </details>
        <hr className="form-divider" />
        <div className="form-section-label">
          <span>۳</span> فایل PDF کتاب (الزامی)
        </div>
        <label className="upload-box">
          <Upload size={27} />
          <b>فایل متن و جلد را انتخاب کنید</b>
          <p>فقط PDF · حداکثر ۱۵ مگابایت برای هر فایل · حداکثر ۵ فایل</p>
          <input
            type="file"
            accept=".pdf,application/pdf"
            multiple
            required
            onChange={async (event) => {
              const input = event.currentTarget, selection = ++fileSelection.current;
              const list = Array.from(input.files || []);
              setFiles([]); setError("");
              try {
                if (list.length > 5 || list.some(f => f.size > 15 * 1024 * 1024 || !/\.pdf$/i.test(f.name)))
                  throw new Error("حداکثر ۵ فایل PDF و هر فایل حداکثر ۱۵ مگابایت مجاز است.");
                for (const file of list) {
                  const [header, tail] = await Promise.all([file.slice(0,8).text(), file.slice(-1024).text()]);
                  if (!header.startsWith("%PDF-") || !tail.includes("%%EOF"))
                    throw new Error("محتوای فایل PDF معتبر نیست.");
                }
                if (selection === fileSelection.current) setFiles(list);
              } catch (error) {
                if (selection === fileSelection.current) { input.value="";setError(error instanceof Error ? error.message : "خواندن فایل انجام نشد."); }
              }
            }}
          />
        </label>
        <label className="check-field" style={{ marginTop: 20 }}>
          <input type="checkbox" required checked={termsAccepted} onChange={event => setTermsAccepted(event.target.checked)} />
          <span>
            با{" "}
            <a href="/privacy" target="_blank" className="text-link">
              شرایط ثبت سفارش و حریم خصوصی
            </a>{" "}
            موافقم.
          </span>
        </label>
        {error && (
          <div className="error-message" role="alert">
            {error}
          </div>
        )}
        <div className="form-actions">
          <button
            disabled={pending || title.trim().length<2 || !termsAccepted || !files.length || !!catalog.error || catalog.loading}
            className="button"
          >
            {pending ? "در حال ثبت درخواست…" : "ارسال درخواست چاپ"}
            <ArrowUpLeft size={19} />
          </button>
          <small>بدون پرداخت آنلاین</small>
        </div>
      </div>
      <QuotePanel
        spec={spec}
        link={false}
      />
    </form>
  );
}
export function TrackingForm() {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<TrackingResult | null>(null);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setPending(true);
    setError("");
    setResult(null);
    const data = new FormData(e.currentTarget);
    try {
      setResult(
        await api<TrackingResult>("track", {
          method: "POST",
          body: JSON.stringify(Object.fromEntries(data)),
        }),
      );
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setPending(false);
    }
  }
  return (
    <div className="two-col">
      <section className="panel">
        <form onSubmit={submit}>
          <div className="panel-title">
            <h2>سفارش من کجاست؟</h2>
            <Search size={20} />
          </div>
          <div className="form-grid">
            <label className="field full">
              کد پیگیری
              <input
                name="code"
                dir="ltr"
                required
                minLength={8}
                maxLength={30}
                placeholder="کد دریافت‌شده هنگام ثبت سفارش"
              />
            </label>

          </div>
          {error && (
            <div role="alert" className="error-message">
              {error}
            </div>
          )}
          <button
            className="button button-full"
            style={{ marginTop: 22 }}
            disabled={pending}
          >
            {pending ? "در حال بررسی…" : "مشاهده وضعیت سفارش"}
            <ArrowUpLeft size={18} />
          </button>
        </form>
        {result && (
          <div className="tracking-result">
            <OrderTrackingDetails result={result} />
          </div>
        )}
      </section>
      <aside className="help-card">
        <h3>هر مرحله، شفاف و قابل پیگیری</h3>
        <p>
          از بررسی فایل تا چاپ، صحافی و آماده تحویل شدن؛ آخرین وضعیت و یادداشت
          کارشناس اینجا نمایش داده می‌شود.
        </p>
        <ul>
          <li>کد پیگیری به حروف بزرگ یا کوچک حساس نیست.</li>
          <li>شماره همراه باید همان شماره ثبت سفارش باشد.</li>
          <li>
            در صورت گم شدن کد، سفارش‌های حساب خود را بررسی کنید یا با چاپخانه
            تماس بگیرید.
          </li>
        </ul>
        <Link prefetch={false} href="/contact" className="text-link">
          ارتباط با پشتیبانی <ArrowUpLeft size={18} />
        </Link>
      </aside>
    </div>
  );
}
export function ContactForm({ customer }: { customer: CustomerProfile }) {
  return (
    <div>
      <p>
        {customer.firstName} {customer.lastName}، از بخش پشتیبانی می‌توانید درخواست ثبت کنید.
       <Link href="/tickets">ورود به پشتیبانی</Link>
      </p>
      <Link prefetch={false} href="/contact">
        راه‌های ارتباط با چاپخانه
      </Link>
    </div>
  );
}
