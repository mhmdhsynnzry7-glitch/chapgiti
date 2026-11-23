import {
  bookSizes,
  bookPapers,
  bookBindings,
  textPrints,
  finishingServices,
  type PrintOptions,
  type BookDetails,
} from "./book-options";
import type { PrintSpec, Rates } from "./pricing";
export function detailsFromSpec(
  spec: PrintSpec,
  options: Pick<PrintOptions, "bookSizes"> = { bookSizes },
): BookDetails {
  const { bookSizes } = options;
  if (spec.details)
    return { ...spec.details, extras: [...spec.details.extras] };
  const dimensions = bookSizes[spec.size];
  if (!dimensions) throw new Error("قطع کتاب در کاتالوگ فعلی در دسترس نیست.");
  return {
    width: dimensions.width,
    height: dimensions.height,
    printType: spec.color === "color" ? "color-toner" : "bw-text",
    sides: "double",
    bwPages: spec.color === "bw" ? spec.pages : 0,
    colorPages: spec.color === "color" ? spec.pages : 0,
    bwPaper:
      spec.paper === "cream"
        ? "bulky"
        : spec.paper === "coated"
          ? "coated128"
          : "white70",
    colorPaper:
      spec.paper === "cream"
        ? "bulky"
        : spec.paper === "coated"
          ? "coated128"
          : "white70",
    printOnly: false,
    bindingStyle:
      spec.binding === "hard"
        ? "hard-flat"
        : spec.binding === "spiral"
          ? "spiral"
          : "hot-glue",
    coverPrint: "front",
    coverPaper: "coated250",
    coating: "gloss",
    orientation: "portrait",
    readingDirection: "rtl",
    extras: [],
  };
}
export function withBookDetails(
  spec: PrintSpec,
  details: BookDetails,
  options: Pick<PrintOptions, "bookPapers" | "bookBindings"> = {
    bookPapers,
    bookBindings,
  },
): PrintSpec {
  const { bookPapers, bookBindings } = options;
  const paper =
    bookPapers[details.bwPages ? details.bwPaper : details.colorPaper];
  return {
    ...spec,
    pages: details.bwPages + details.colorPages,
    color: details.colorPages ? "color" : "bw",
    paper:
      paper?.type === "cream"
        ? "cream"
        : paper?.type === "coated" || paper?.type === "label"
          ? "coated"
          : "white",
    binding: bookBindings[details.bindingStyle]?.base ?? spec.binding,
    details,
  };
}
export function bookSpine(
  spec: PrintSpec,
  options: Pick<PrintOptions, "bookPapers" | "bookSizes"> = {
    bookPapers,
    bookSizes,
  },
) {
  const { bookPapers } = options;
  const d = detailsFromSpec(spec, options),
    sides = d.sides === "double" ? 2 : 1;
  return (
    Math.round(
      ((d.bwPages * bookPapers[d.bwPaper].caliper +
        d.colorPages * bookPapers[d.colorPaper].caliper) /
        sides) *
        10,
    ) / 10
  );
}
// Input amounts are whole toman. Divide after multiplying, as in QuantityDiscount.Savings.
export function quantitySavings(subtotal: number, percentage: number) {
  const percent = Math.max(0, Math.min(100, Math.trunc(percentage)));
  return Number(BigInt(subtotal) * BigInt(percent) / BigInt(100));
}
export function quoteBook(
  spec: PrintSpec,
  rates: Rates,
  options?: PrintOptions,
  discountPercent = 0,
) {
  const sizes = options?.bookSizes ?? bookSizes,
    papers = options?.bookPapers ?? bookPapers,
    bindings = options?.bookBindings ?? bookBindings,
    prints = options?.textPrints ?? textPrints,
    finishes = options?.finishingServices ?? finishingServices;
  const d = detailsFromSpec(spec, { bookSizes: sizes }),
    mode = prints[d.printType];
  const sizeFactor =
    (sizes[spec.size].isCustom ?? spec.size === "custom")
      ? Math.max(0.5, (d.width * d.height) / (14.5 * 21.5))
      : 1;
  const paperExtra = (id: keyof typeof bookPapers) => {
    const p = papers[id];
    if (p.type === "cream") return rates.cream;
    if (p.type === "coated") return (rates.coated * p.grams) / 115;
    if (p.type === "label") return rates.coated * 2;
    if (p.type === "colored") return rates.cream * 0.7;
    return Math.max(0, (p.grams / 70 - 1) * rates.bw);
  };
  const sideFactor = d.sides === "single" ? 2 : 1;
  const pageCost = (id: keyof typeof bookPapers, ink: number) => {
    const paper = rates.bw * 0.28 + paperExtra(id);
    return Math.max(0, ink - rates.bw * 0.28) + paper * sideFactor;
  };
  const bwInk =
    (mode.noPrint ?? d.printType === "no-print") ? 0 : rates.bw * mode.bwFactor;
  const print = Math.ceil(
    (d.bwPages * pageCost(d.bwPaper, bwInk) +
      d.colorPages * pageCost(d.colorPaper, rates.color * mode.colorFactor)) *
      sizeFactor,
  );
  const bindingInfo = bindings[d.bindingStyle];
  const binding = d.printOnly
    ? 0
    : Math.ceil(
        (rates[bindingInfo.base] * bindingInfo.factor +
          ((bindingInfo.extraSpiral ?? d.bindingStyle === "hard-spiral")
            ? rates.spiral
            : 0)) *
          (d.orientation === "landscape" ? 1.05 : 1),
      );
  const cp = options?.coverPrints[d.coverPrint],
    paper = options?.coverPapers[d.coverPaper],
    coating = options?.coverCoatings[d.coating];
  const coverPrintFactor =
    (typeof cp === "object" ? cp.factor : undefined) ??
    (
      {
        front: 1,
        both: 2,
        flap: 1.35,
        "both-flap": 2.4,
        none: 0,
        plastic: 0,
      } as Record<string, number>
    )[d.coverPrint];
  const coverPaperFactor =
    (typeof paper === "object" ? paper.factor : undefined) ??
    (
      {
        coated300: 1.2,
        coated250: 1,
        coated200: 0.8,
        kraft270: 1.15,
        plastic: 1.3,
      } as Record<string, number>
    )[d.coverPaper];
  const cover =
    d.printOnly || (typeof cp === "object" ? cp.none : d.coverPrint === "none")
      ? 0
      : Math.ceil(
          (rates.coverPrint * coverPrintFactor +
            rates.coverPaper * coverPaperFactor +
            ((typeof coating === "object"
              ? coating.laminated
              : d.coating !== "none") &&
            !(typeof cp === "object" ? cp.plastic : d.coverPrint === "plastic")
              ? rates.lamination
              : 0)) *
            sizeFactor,
        );
  const finishing = d.extras.map((key) => ({
    key,
    label: finishes[key].label,
    unit: Math.ceil(
      rates[(finishes[key].rateKey ?? key) as keyof Rates],
    ),
  }));
  const extras = finishing.reduce((sum, row) => sum + row.unit, 0);
  const subtotal = (print + binding + cover + extras) * spec.quantity;
  const discountAmount = quantitySavings(subtotal, discountPercent),
    total = subtotal - discountAmount + rates.setup;
  return {
    print,
    binding,
    cover,
    extras,
    finishing,
    setup: rates.setup,
    subtotal,
    discountAmount,
    total,
    unit: Math.ceil(total / spec.quantity),
    spine: bookSpine(spec, { bookPapers: papers, bookSizes: sizes }),
  };
}
