import type { PrintOptions, BookDetails } from "./book-options";
import type { PrintSpec } from "./pricing";
export const catalogKinds = {
  bookSizes: "قطع‌های کتاب",
  textPrints: "انواع چاپ متن",
  bookPapers: "کاغذ متن",
  bookBindings: "صحافی",
  coverPrints: "چاپ جلد",
  coverPapers: "کاغذ جلد",
  coverCoatings: "پوشش جلد",
  finishingServices: "خدمات تکمیلی",
} as const;
export type CatalogKind = keyof typeof catalogKinds;
export type CatalogItem = {
  id: string;
  kind: CatalogKind;
  code: string;
  label: string;
  pricingRule: string;
  metadata: Record<string, unknown>;
  displayOrder: number;
  active: boolean;
  isDefault: boolean;
  version: number;
  createdAt: string;
  updatedAt: string;
};
export function catalogOptions(items: CatalogItem[]): PrintOptions {
  const groups = Object.fromEntries(
    Object.keys(catalogKinds).map((kind) => [
      kind,
      Object.fromEntries(
        items
          .filter((i) => i.kind === kind && i.active)
          .map((i) => [i.code, { ...i.metadata, label: i.label }]),
      ),
    ]),
  );
  if (Object.values(groups).some((group) => !Object.keys(group).length))
    throw new Error("تنظیمات چاپ کامل نیست؛ با چاپخانه تماس بگیرید.");
  return groups as PrintOptions;
}
export function catalogDefaults(
  items: CatalogItem[],
  options: PrintOptions,
): PrintSpec {
  const code = (kind: CatalogKind) => {
    const found = items.find((i) => i.kind === kind && i.active && i.isDefault);
    if (!found) throw new Error("گزینه پیش‌فرض چاپ تنظیم نشده است.");
    return found.code;
  };
  const size = code("bookSizes"),
    printType = code("textPrints"),
    paper = code("bookPapers"),
    bindingStyle = code("bookBindings"),
    mode = options.textPrints[printType];
  const details: BookDetails = {
    width: options.bookSizes[size].width,
    height: options.bookSizes[size].height,
    printType,
    sides: "double",
    bwPages: mode.bw ? 120 : 0,
    colorPages: mode.bw ? 0 : 120,
    bwPaper: paper,
    colorPaper: paper,
    printOnly: false,
    bindingStyle,
    coverPrint: code("coverPrints"),
    coverPaper: code("coverPapers"),
    coating: code("coverCoatings"),
    orientation: "portrait",
    readingDirection: "rtl",
    extras: [],
  };
  const type = options.bookPapers[paper].type;
  return {
    pages: 120,
    quantity: 100,
    size,
    paper:
      type === "cream"
        ? "cream"
        : type === "coated" || type === "label"
          ? "coated"
          : "white",
    color: mode.bw ? "bw" : "color",
    binding: options.bookBindings[bindingStyle].base,
    details,
  };
}
/** Old technical URL parameters are overlays; fresh forms always start with SQL defaults. */
export function catalogInitialSpec(
  items: CatalogItem[],
  options: PrintOptions,
  overrides?: Partial<PrintSpec>,
): PrintSpec {
  const defaults = catalogDefaults(items, options);
  if (!overrides) return defaults;
  if (overrides.details)
    return {
      ...defaults,
      ...overrides,
      details: { ...overrides.details, extras: [...overrides.details.extras] },
    };
  const spec = { ...defaults, ...overrides },
    details = { ...defaults.details!, extras: [] };
  if (overrides.size && Object.hasOwn(options.bookSizes, overrides.size)) {
    const format = options.bookSizes[overrides.size];
    details.width = format.width;
    details.height = format.height;
  }
  // Compatibility mapping for explicit historical base enums, never a list of available choices.
  if (overrides.color)
    details.printType = overrides.color === "color" ? "color-toner" : "bw-text";
  if (overrides.paper)
    details.bwPaper = details.colorPaper =
      overrides.paper === "cream"
        ? "bulky"
        : overrides.paper === "coated"
          ? "coated128"
          : "white70";
  if (overrides.binding)
    details.bindingStyle =
      overrides.binding === "hard"
        ? "hard-flat"
        : overrides.binding === "spiral"
          ? "spiral"
          : "hot-glue";
  const mode = options.textPrints[details.printType];
  details.bwPages = mode
    ? mode.bw
      ? spec.pages
      : 0
    : spec.color === "bw"
      ? spec.pages
      : 0;
  details.colorPages = spec.pages - details.bwPages;
  const paper =
    options.bookPapers[details.bwPages ? details.bwPaper : details.colorPaper];
  return {
    ...spec,
    color: details.colorPages ? "color" : "bw",
    paper: paper
      ? paper.type === "cream"
        ? "cream"
        : paper.type === "coated" || paper.type === "label"
          ? "coated"
          : "white"
      : spec.paper,
    binding: options.bookBindings[details.bindingStyle]?.base ?? spec.binding,
    details,
  };
}
export function attachCatalogIds(spec: PrintSpec, items: CatalogItem[]) {
  const selected = new Set(["bookSizes:" + spec.size]);
  if (spec.details) {
    const d = spec.details;
    for (const [field, kind] of [
      ["printType", "textPrints"],
      ["bwPaper", "bookPapers"],
      ["colorPaper", "bookPapers"],
      ["bindingStyle", "bookBindings"],
      ["coverPrint", "coverPrints"],
      ["coverPaper", "coverPapers"],
      ["coating", "coverCoatings"],
    ] as const)
      selected.add(kind + ":" + d[field]);
    for (const code of d.extras) selected.add("finishingServices:" + code);
  }
  return {
    ...spec,
    catalogIds: Object.fromEntries(
      items
        .filter((i) => i.active && selected.has(i.kind + ":" + i.code))
        .map((i) => [i.kind + ":" + i.code, i.id]),
    ),
  };
}
export function optionLabel(value: string | { label: string }) {
  return typeof value === "string" ? value : value.label;
}
export function catalogChanged() {
  window.dispatchEvent(new Event("chap-print-catalog-changed"));
  if ("BroadcastChannel" in window) {
    const channel = new BroadcastChannel("chap-print-catalog");
    channel.postMessage("changed");
    channel.close();
  }
}
