"use client";
import { formatToman } from "@/lib/money";
import { sessionFetch } from "@/lib/session-client";
import Link from "next/link";
import PrintRequestLink from "./print-request-link";
import { useEffect, useRef, useState } from "react";
import {
  Calculator,
  ArrowUpLeft,
  RotateCcw,
  BookOpen,
  UserRound,
  CheckCircle2,
} from "lucide-react";
import BookSpecFields from "./book-spec-fields";
import { money, type PrintSpec } from "@/lib/pricing";
import type { CustomerProfile } from "@/lib/customer";
import { validatePrintSpec } from "@/lib/print-spec";
import { usePrintCatalog, CatalogStatus } from "./print-catalog-provider";
import { catalogDefaults, attachCatalogIds } from "@/lib/print-catalog";
type Calculation = {
  unitBeforeDiscount: number;
  subtotal: number;
  discountPercent: number;
  unit: number;
  total: number;
  spine: number;
};
type CalculatorProps = {
  customer: CustomerProfile | null;
};
export default function BookCalculator(props: CalculatorProps) {
  const catalog = usePrintCatalog();
  if (!catalog.options) return <CatalogStatus />;
  return <CalculatorFields {...props} />;
}
function CalculatorFields({ customer }: CalculatorProps) {
  const catalog = usePrintCatalog(),
    options = catalog.options!;
  const [spec, setSpec] = useState<PrintSpec>(() =>
    catalogDefaults(catalog.items, options),
  );
  const [result, setResult] = useState<Calculation | null>(null),
    [fingerprint, setFingerprint] = useState("");
  const [busy, setBusy] = useState(false),
    [error, setError] = useState("");
  const [expired,setExpired] = useState(false);
  const currentFingerprint = JSON.stringify({
    ...spec,
    catalogVersions: catalog.items.map((i) => [i.id, i.version]),
  });
  const fresh =
    !!result && !catalog.error && fingerprint === currentFingerprint;
  const pending = useRef<AbortController | null>(null);
  const attempted = useRef(false);
  useEffect(() => () => pending.current?.abort(), []);

  async function calculate(input: PrintSpec) {
    pending.current?.abort();
    const controller = new AbortController();
    pending.current = controller;
    attempted.current = true;
    setBusy(true);
    setError("");
    setResult(null);
    setExpired(false);
    try {
      const body = attachCatalogIds(validatePrintSpec(input, options), catalog.items);
      const response = await sessionFetch("/api/quote", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body), signal: controller.signal,
      });
      const data = await response.json();
      if (controller.signal.aborted) return;
      if (response.status === 401) {
        setExpired(true);
        throw new Error("نشست شما منقضی شده است. برای محاسبه دوباره وارد شوید.");
      }
      if (!response.ok) throw new Error(data.error || "محاسبه انجام نشد.");
      setResult(data);
      setFingerprint(JSON.stringify({...input, catalogVersions: catalog.items.map(i => [i.id,i.version])}));
    } catch (cause) {
      if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "ارتباط برقرار نشد.");
    } finally {
      if (!controller.signal.aborted) setBusy(false);
    }
  }
  function changeSpec(next: PrintSpec) {
    pending.current?.abort();
    setSpec(next);
    setResult(null);
    setBusy(false);
    setError("");
    if (attempted.current && next.size !== spec.size) void calculate(next);
  }
  return (
    <>
      {customer && (
        <div className="calculator-customer">
          <span>
            <UserRound size={19} />
            {customer.firstName} {customer.lastName}
            <small dir="ltr">{customer.phone}</small>
          </span>
          <Link prefetch={false} href="/customer/profile?next=/calculator">
            ویرایش مشخصات
          </Link>
        </div>
      )}
      <form className="book-calculator" onSubmit={event=>{event.preventDefault();void calculate(spec);}}>
        <div className="calculator-form-column">
          <BookSpecFields spec={spec} onChange={changeSpec} />
          {error && (
            <div className="error-message" role="alert">
              <p>{error}</p>
              <Link prefetch={false} className="text-link" href={expired ? "/customer?next=%2Fcalculator" : "/contact"}>
                {expired ? "ورود دوباره" : "درخواست مشاوره"}
              </Link>
            </div>
          )}
          <div className="calculation-actions">
            <button
              className="button"
              disabled={busy || !!catalog.error || catalog.loading}
            >
              <Calculator size={19} />
              {busy ? "در حال محاسبه…" : "محاسبه هزینه"}
            </button>
            <button
              type="button"
              className="button button-outline"
              onClick={() => {
                attempted.current=false;
                changeSpec(catalogDefaults(catalog.items, options));
                setResult(null);
                setError("");
              }}
            >
              <RotateCcw size={17} />
              شروع دوباره
            </button>
          </div>
        </div>
        <aside className="calculation-result" aria-live="polite">
          <div className="result-heading">
            <span className="result-icon">
              <Calculator size={23} />
            </span>
            <div>
              <h2>نتیجه قیمت</h2>
              <span>مبالغ به تومان</span>
            </div>
          </div>
          {!fresh || !result ? (
            <div className="calculation-empty">
              <BookOpen size={48} />
              <h3>کتابتان را تعریف کنید</h3>
              <p>
                فرم را تکمیل کنید و «محاسبه هزینه» را بزنید تا مبلغ برآورد نمایش
                داده شود.
              </p>
              <div className="result-mini-book" aria-hidden="true">
                <i />
                <b>
                  کتاب
                  <br />
                  شما
                </b>
              </div>
            </div>
          ) : (
            <>
              <div className="result-values">
                <dl className="price-summary">
                  <div><dt>قیمت هر جلد قبل از تخفیف</dt><dd>{formatToman(result.unitBeforeDiscount)}</dd></div>
                  <div><dt>جمع مبلغ مشمول تخفیف</dt><dd>{formatToman(result.subtotal)}</dd></div>
                  <div><dt>تخفیف تیراژ</dt><dd>{result.discountPercent ? result.discountPercent.toLocaleString("fa-IR")+"٪" : "بدون تخفیف"}</dd></div>
                  <div><dt>قیمت هر جلد بعد از تخفیف</dt><dd>{formatToman(result.unit)}</dd></div>
                  <div><dt>مبلغ نهایی</dt><dd><strong>{formatToman(result.total)}</strong></dd></div>
                </dl>
                <div className="spine-estimate">
                  <BookOpen size={16} />
                  عطف تقریبی: <b>{money(result.spine)} میلی‌متر</b>
                </div>
              </div>
              {fresh && (
                <PrintRequestLink className="button button-full" spec={spec}>
                  ارسال درخواست چاپ
                  <ArrowUpLeft size={18} />
                </PrintRequestLink>
              )}
            </>
          )}
          <div className="estimate-note">
            <CheckCircle2 size={17} />
            <p>
              قیمت نهایی پس از بررسی فایل و تأیید مشخصات اعلام می‌شود. طراحی،
              خدمات نشر و ارسال جداگانه محاسبه می‌شوند.
            </p>
          </div>
        </aside>
      </form>
    </>
  );
}
