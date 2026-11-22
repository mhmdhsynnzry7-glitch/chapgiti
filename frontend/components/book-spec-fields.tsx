"use client";
import { type BookDetails } from "@/lib/book-options";
import { detailsFromSpec, withBookDetails } from "@/lib/book-pricing";
import { money, type PrintSpec } from "@/lib/pricing";
import { useState } from "react";
import { finishingUnavailable } from "@/lib/finishing-compatibility";
import CountInput from "./count-input";
import CatalogSelect from "./catalog-select";
import { optionLabel } from "@/lib/print-catalog";
import { usePrintCatalog, CatalogStatus } from "./print-catalog-provider";
export default function BookSpecFields({
  spec,
  onChange,
  order = false,
}: {
  spec: PrintSpec;
  onChange: (spec: PrintSpec) => void;
  order?: boolean;
}) {
  const [removed, setRemoved] = useState("");
  const catalog = usePrintCatalog();
  if (!catalog.options) return <CatalogStatus />;
  const {
    bookSizes,
    textPrints,
    bookPapers,
    bookBindings,
    coverPrints,
    coverPapers,
    coverCoatings,
    finishingServices,
  } = catalog.options;
  const d = detailsFromSpec(spec, catalog.options),
    mode = textPrints[d.printType] ?? { bw: false, color: false };
  const format = bookSizes[spec.size];
  const custom = format?.isCustom ?? false;
  const dimensionsChanged =
    !!format &&
    !custom &&
    (Math.abs(d.width - format.width) > 0.01 ||
      Math.abs(d.height - format.height) > 0.01);
  function update(patch: Partial<BookDetails>, base: Partial<PrintSpec> = {}) {
    const next = { ...d, ...patch };
    const unavailable = next.extras.filter((key) =>
      finishingUnavailable(key, next, catalog.options!),
    );
    setRemoved(
      unavailable.length
        ? "به دلیل تغییر مشخصات، این خدمات حذف شدند: " +
            unavailable
              .map((key) => finishingServices[key]?.label ?? key)
              .join("، ")
        : "",
    );
    next.extras = next.extras.filter(
      (key) => !finishingUnavailable(key, next, catalog.options!),
    );
    onChange(withBookDetails({ ...spec, ...base }, next, catalog.options!));
  }
  function changePrintType(value: BookDetails["printType"]) {
    const nextMode = textPrints[value],
      total = Math.max(spec.pages, 4);
    const bwPages = nextMode.bw
      ? nextMode.color
        ? Math.min(total, Math.max(0, d.bwPages))
        : total
      : 0;
    update({
      printType: value,
      bwPages,
      colorPages: nextMode.color ? total - bwPages : 0,
    });
  }
  const select = (
    label: string,
    key: keyof BookDetails,
    options: Record<string, string | { label: string }>,
    disabled = false,
  ) => (
    <CatalogSelect
      key={key}
      label={label}
      value={String(d[key])}
      disabled={disabled}
      onChange={(value) => update({ [key]: value } as Partial<BookDetails>)}
      options={Object.entries(options).map(([value, item]) => ({
        value,
        label: optionLabel(item),
      }))}
    />
  );
  const paperOptions = Object.fromEntries(
    Object.entries(bookPapers).map(([key, value]) => [key, value.label]),
  );
  return (
    <div className="book-spec-fields">
      {dimensionsChanged && (
        <div className="notice" role="status">
          ابعاد این قطع در تنظیمات چاپ تغییر کرده است. پیش از محاسبه، ابعاد تازه
          را تأیید کنید.
          <button
            type="button"
            className="button button-sm button-outline"
            onClick={() =>
              update({ width: format.width, height: format.height })
            }
          >
            اعمال ابعاد جدید
          </button>
        </div>
      )}
      {catalog.error && <CatalogStatus />}
      <section className="spec-section" aria-labelledby="spec-size-title">
        <div className="spec-section-heading">
          <div>
            <h2 id="spec-size-title">قطع و ابعاد کتاب</h2>
            <p>اندازه نهایی کتاب پس از برش</p>
          </div>
        </div>
        <div className="spec-grid">
          <CatalogSelect
            label="قطع کتاب"
            value={spec.size}
            onChange={(size) => {
              const dimensions = bookSizes[size];
              update(
                { width: dimensions.width, height: dimensions.height },
                { size },
              );
            }}
            options={Object.entries(bookSizes).map(([value, item]) => ({
              value,
              label: item.label,
              description: item.isCustom
                ? "ابعاد سفارشی در بازه مجاز"
                : item.width + " × " + item.height + " سانتی‌متر",
            }))}
          />
          <label className="field">
            طول کتاب (سانتی‌متر)
            <input
              type="number"
              min={format?.minHeight ?? 8}
              max={format?.maxHeight ?? 45}
              step={0.1}
              required
              value={d.height || ""}
              readOnly={!custom}
              onChange={(event) =>
                update({ height: Number(event.target.value) })
              }
            />
          </label>
          <label className="field">
            عرض کتاب (سانتی‌متر)
            <input
              type="number"
              min={format?.minWidth ?? 8}
              max={format?.maxWidth ?? 45}
              step={0.1}
              required
              value={d.width || ""}
              readOnly={!custom}
              onChange={(event) =>
                update({ width: Number(event.target.value) })
              }
            />
          </label>
        </div>
      </section>
      <section className="spec-section" aria-labelledby="spec-text-title">
        <div className="spec-section-heading">
          <div>
            <h2 id="spec-text-title">چاپ متن و کاغذ</h2>
            <p>صفحات سیاه و سفید و رنگی را جداگانه وارد کنید.</p>
          </div>
        </div>
        <div className="spec-grid two">
          <CatalogSelect
            label="چاپ متن"
            value={d.printType}
            onChange={changePrintType}
            options={Object.entries(textPrints).map(([value, item]) => ({
              value,
              label: item.label,
            }))}
          />
          {select("وجه چاپ", "sides", { double: "دو رو", single: "یک رو" })}
          {mode.bw && <label className="field">
            {d.printType === "no-print"
              ? "تعداد صفحات بدون چاپ"
              : "تعداد صفحات سیاه و سفید"}
            <CountInput
              min={0}
              max={2000}
              required
              disabled={!mode.bw}
              value={d.bwPages}
              onValueChange={(bwPages) => update({ bwPages })}
            />
          </label>}
          {mode.color && <label className="field">
            تعداد صفحات رنگی / دو رنگ
            <CountInput
              min={0}
              max={2000}
              required
              disabled={!mode.color}
              value={d.colorPages}
              onValueChange={(colorPages) => update({ colorPages })}
            />
          </label>}
          {mode.bw && select("کاغذ متن سیاه و سفید", "bwPaper", paperOptions, !mode.bw)}
          {mode.color && select("کاغذ متن رنگی", "colorPaper", paperOptions, !mode.color)}
        </div>
        <div className="page-count-summary">
          مجموع صفحات هر جلد{" "}
          <strong>
            {Number.isFinite(spec.pages) ? money(spec.pages) : "—"} صفحه
          </strong>
          <small>
            هر جلد بین ۴ تا ۲۰۰۰ صفحه؛ هر برگ دورو شامل دو صفحه است.
          </small>
        </div>
      </section>
      {!d.printOnly && <section className="spec-section" aria-labelledby="spec-cover-title"><div className="spec-section-heading"><h2 id="spec-cover-title">جلد و چاپ جلد</h2></div><div className="spec-grid two">
          {select("چاپ جلد", "coverPrint", coverPrints, d.printOnly)}
          {d.coverPrint !== "none" && select(
            "کاغذ جلد",
            "coverPaper",
            coverPapers,
            d.printOnly || d.coverPrint === "none",
          )}
          {!["none","plastic"].includes(d.coverPrint) && select(
            "پوشش جلد",
            "coating",
            coverCoatings,
            d.printOnly ||
              d.coverPrint === "none" ||
              d.coverPrint === "plastic",
          )}
      </div></section>}
      <section className="spec-section" aria-labelledby="spec-binding-title">
        <div className="spec-section-heading">
          <div>
            <h2 id="spec-binding-title">صحافی</h2>
            <p>انتخاب ظاهر و ماندگاری کتاب</p>
          </div>
          <label className="print-only">
            <input
              type="checkbox"
              checked={d.printOnly}
              onChange={(event) => update({ printOnly: event.target.checked })}
            />{" "}
            فقط چاپ، بدون صحافی
          </label>
        </div>
        <div className="spec-grid two">
          {select(
            "نوع صحافی",
            "bindingStyle",
            Object.fromEntries(
              Object.entries(bookBindings).map(([key, value]) => [
                key,
                value.label,
              ]),
            ),
            d.printOnly,
          )}
          {select(
            "جهت صحافی",
            "orientation",
            { portrait: "عمودی", landscape: "افقی (بیاضی)" },
            d.printOnly,
          )}
          {select("جهت خواندن", "readingDirection", {
            rtl: "راست به چپ (فارسی)",
            ltr: "چپ به راست (انگلیسی)",
          })}
        </div>
      </section>

      {!d.printOnly && d.coverPrint!=="none" && <details className="spec-section optional-finishes"
        id="finishing-options"
        aria-labelledby="spec-extras-title"
      >
        <summary id="spec-extras-title">خدمات تکمیلی (اختیاری){d.extras.length > 0 && " · " + d.extras.length.toLocaleString("fa-IR") + " انتخاب"}</summary>
        <div className="finishing-options">
          {Object.entries(finishingServices).map(([key, item]) => {
            const id = key as keyof typeof finishingServices;
            const reason = finishingUnavailable(id, d, catalog.options!);
            const disabled = !!reason;
            return (
              <label
                className={
                  "finishing-option " +
                  (d.extras.includes(id) ? "selected " : "") +
                  (disabled ? "disabled" : "")
                }
                key={key}
              >
                <input
                  aria-label={item.label}
                  aria-describedby={
                    reason ? "finishing-reason-" + key : undefined
                  }
                  type="checkbox"
                  checked={d.extras.includes(id)}
                  disabled={disabled}
                  onChange={(event) =>
                    update({
                      extras: event.target.checked
                        ? [...d.extras, id]
                        : d.extras.filter((value) => value !== id),
                    })
                  }
                />
                <span>
                  {item.label}
                  {reason && (
                    <small id={"finishing-reason-" + key}>{reason}</small>
                  )}
                </span>
              </label>
            );
          })}
        </div>
        {removed && (
          <p className="notice" role="status">
            {removed}
          </p>
        )}
        <p className="spec-hint">
          طلاکوب، نقره‌کوب و یووی موضعی فقط با سلفون مات قابل انتخاب‌اند. خدمات
          جلد سخت با انتخاب صحافی جلد سخت فعال می‌شوند.
        </p>
      </details>}

      <section className="spec-section quantity-section">
        <h2>تیراژ و صفحات</h2>
        <label className="field">
          تعداد جلد (تیراژ)
          <CountInput
            aria-label="تعداد جلد (تیراژ)"
            min={1}
            max={10000}
            required
            value={spec.quantity}
            onValueChange={(quantity) => onChange({ ...spec, quantity })}
          />
          <small>از یک تا ۱۰٬۰۰۰ جلد</small>
        </label>
        <p>
          {order
            ? "برآورد کنار فرم با مشخصات انتخابی شما به‌روز می‌شود."
            : "مشخصات را بررسی کنید و برای دریافت برآورد، دکمه «محاسبه هزینه» را بزنید."}
        </p>
      </section>
    </div>
  );
}
