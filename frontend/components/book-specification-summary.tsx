import {
  bookBindings,
  bookPapers,
  textPrints,
  coverPrints,
  coverPapers,
  coverCoatings,
  finishingServices,
} from "@/lib/book-options";
import type { PrintSpec } from "@/lib/pricing";
import { money } from "@/lib/pricing";
import { optionLabel } from "@/lib/print-catalog";
export default function BookSpecificationSummary({
  spec,
}: {
  spec: Pick<PrintSpec, "details" | "catalogSnapshot">;
}) {
  const d = spec.details;
  if (!d) return null;
  const label = (
    kind: string,
    code: string,
    legacy?: string | { label: string },
  ) =>
    spec.catalogSnapshot?.options.find(
      (i) => i.kind === kind && i.code === code,
    )?.label ?? (legacy ? optionLabel(legacy) : code);
  const rows = [
    ["ابعاد", money(d.height) + " × " + money(d.width) + " سانتی‌متر"],
    ["چاپ متن", label("textPrints", d.printType, textPrints[d.printType])],
    ["وجه چاپ", d.sides === "double" ? "دو رو" : "یک رو"],
    ["صفحات سیاه و سفید", money(d.bwPages)],
    ["صفحات رنگی", money(d.colorPages)],
    ["کاغذ سیاه و سفید", label("bookPapers", d.bwPaper, bookPapers[d.bwPaper])],
    ["کاغذ رنگی", label("bookPapers", d.colorPaper, bookPapers[d.colorPaper])],
    [
      "صحافی",
      d.printOnly
        ? "فقط چاپ"
        : label("bookBindings", d.bindingStyle, bookBindings[d.bindingStyle]),
    ],
    [
      "چاپ جلد",
      d.printOnly
        ? "بدون جلد"
        : label("coverPrints", d.coverPrint, coverPrints[d.coverPrint]),
    ],
    [
      "کاغذ جلد",
      d.printOnly || d.coverPrint === "none"
        ? "—"
        : label("coverPapers", d.coverPaper, coverPapers[d.coverPaper]),
    ],
    [
      "پوشش",
      d.printOnly
        ? "—"
        : label("coverCoatings", d.coating, coverCoatings[d.coating]),
    ],
    ["جهت صحافی", d.orientation === "portrait" ? "عمودی" : "افقی"],
    ["جهت خواندن", d.readingDirection === "rtl" ? "راست به چپ" : "چپ به راست"],
    [
      "خدمات تکمیلی",
      d.extras.length
        ? d.extras
            .map((key) =>
              label("finishingServices", key, finishingServices[key]),
            )
            .join("، ")
        : "ندارد",
    ],
  ];
  return (
    <div className="order-spec-details">
      {rows.map(([label, value]) => (
        <div key={label}>
          <span>{label}</span>
          <b>{value}</b>
        </div>
      ))}
    </div>
  );
}
