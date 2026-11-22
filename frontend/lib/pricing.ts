import { bookSizes, type BookDetails } from "./book-options";
import { quoteBook } from "./book-pricing";
export type Rates = Record<
  "bw" | "color" | "cream" | "coated" | "soft" | "hard" | "spiral" | "setup" | "coverPrint" | "coverPaper" | "lamination" |
  "roundCorners" | "shrink" | "goldFoil" | "silverFoil" | "spotUV" | "endpaperBw" | "endpaperColor" |
  "bookmark" | "coloredEdges" | "metallicEdges" | "bellyBand" | "thickEdges" | "hardCoverJacket" | "ribbon" | "laminatedEndpaper", number
>;
export type PrintSpec = {
  pages: number;
  quantity: number;
  size: string;
  details?: BookDetails;
  catalogSnapshot?: {
    version: number;
    options: {
      id: string;
      kind: string;
      code: string;
      label: string;
      version: number;
      metadata: Record<string, unknown>;
    }[];
  };
  paper: "white" | "cream" | "coated";
  color: "bw" | "color";
  binding: "soft" | "hard" | "spiral";
};
export const defaultSpec: PrintSpec = {
  pages: 120,
  quantity: 100,
  size: "raghi",
  paper: "white",
  color: "bw",
  binding: "soft",
};
export const labels = {
  size: Object.fromEntries(
    Object.entries(bookSizes).map(([key, value]) => [key, value.label]),
  ) as Record<keyof typeof bookSizes, string>,
  paper: {
    white: "تحریر سفید ۷۰ گرم",
    cream: "بالکی کرم ۶۰ گرم",
    coated: "گلاسه ۱۱۵ گرم",
  },
  color: { bw: "سیاه و سفید", color: "تمام‌رنگی" },
  binding: { soft: "شومیز", hard: "جلد سخت", spiral: "فنر مارپیچ" },
};
export function quote(spec: PrintSpec, rates: Rates, discountPercent = 0) {
  if (spec.details) return quoteBook(spec, rates, undefined, discountPercent);
  const paperExtra =
    spec.paper === "cream"
      ? rates.cream
      : spec.paper === "coated"
        ? rates.coated
        : 0;
  const print = Math.ceil(
    spec.pages * (rates[spec.color] + paperExtra),
  );
  const binding = rates[spec.binding];
  const subtotal = (print + binding) * spec.quantity;
  const discount = Math.max(0, Math.min(100, Math.trunc(discountPercent))) / 100;
  const discountAmount = Math.floor(subtotal * discount);
  const total = subtotal - discountAmount + rates.setup;
  return {
    print,
    binding,
    cover: 0,
    extras: 0,
    finishing: [] as { key: string; label: string; unit: number }[],
    spine: 0,
    setup: rates.setup,
    subtotal,
    discountAmount,
    total,
    unit: Math.ceil(total / spec.quantity),
  };
}
export const money = (n: number) => new Intl.NumberFormat("fa-IR").format(n);
export const date = (s: string) =>
  new Intl.DateTimeFormat("fa-IR", {
    dateStyle: "medium",
    timeZone: "Asia/Tehran",
  }).format(new Date(s));
export const statuses: Record<string, string> = {
  new: "در انتظار بررسی",
  review: "در حال بررسی چاپخانه",
  awaiting_customer_approval: "منتظر تأیید شما",
  confirmed: "قیمت تأییدشده",
  printing: "در حال چاپ",
  binding: "صحافی",
  ready: "آماده تحویل",
  completed: "تحویل شده",
  cancelled: "رد / لغو شده",
};

export const adminStatuses: Record<string, string> = {
  ...statuses,
  awaiting_customer_approval: "در انتظار تأیید مشتری",
  confirmed: "قیمت تأییدشده",
};

export function normalizeDigits(s: string) {
  return s
    .replace(/[۰-۹]/g, (d) => String("۰۱۲۳۴۵۶۷۸۹".indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String("٠١٢٣٤٥٦٧٨٩".indexOf(d)));
}
export function csvCell(v: unknown) {
  let s = String(v ?? "");
  if (/^[=+@\-\t\r]/.test(s)) s = "'" + s;
  return '"' + s.replaceAll('"', '""') + '"';
}
