// Legacy option values are retained only for historical read models and isolated parity tests.
// Interactive forms receive the SQL catalog; these records are never their authority.
export type FormatOption = {
  label: string;
  width: number;
  height: number;
  factor: number;
  unit?: string;
  isCustom?: boolean;
  minWidth?: number;
  maxWidth?: number;
  minHeight?: number;
  maxHeight?: number;
};
export type TextPrintOption = {
  label: string;
  bw: boolean;
  color: boolean;
  bwFactor: number;
  colorFactor: number;
  technology?: string;
  noPrint?: boolean;
};
export type PaperOption = {
  label: string;
  grams: number;
  type: "white" | "cream" | "coated" | "label" | "colored";
  caliper: number;
};
export type BindingOption = {
  label: string;
  base: "soft" | "hard" | "spiral";
  factor: number;
  hard: boolean;
  extraSpiral?: boolean;
};
export type CoverOption = {
  label: string;
  factor?: number;
  none?: boolean;
  plastic?: boolean;
  laminated?: boolean;
  matte?: boolean;
};
export type FinishingOption = {
  label: string;
  matte: boolean;
  hard: boolean;
  rateKey?: string;
};
export type PrintOptions = {
  bookSizes: Record<string, FormatOption>;
  textPrints: Record<string, TextPrintOption>;
  bookPapers: Record<string, PaperOption>;
  bookBindings: Record<string, BindingOption>;
  coverPrints: Record<string, string | CoverOption>;
  coverPapers: Record<string, string | CoverOption>;
  coverCoatings: Record<string, string | CoverOption>;
  finishingServices: Record<string, FinishingOption>;
};
export const bookSizes: PrintOptions["bookSizes"] = {
  raghi: { label: "رقعی استاندارد", width: 14.5, height: 21.5, factor: 1 },
  vaziri: { label: "وزیری", width: 17, height: 24, factor: 1.2 },
  a4: { label: "رحلی", width: 21, height: 29.7, factor: 1.7 },
  palto: { label: "پالتویی", width: 11, height: 22, factor: 0.9 },
  pocket: { label: "جیبی وزیری", width: 12, height: 17, factor: 0.8 },
  square21: { label: "خشتی ۲۱ × ۲۱", width: 21, height: 21, factor: 1.45 },
  square16: { label: "خشتی ۱۶ × ۱۶", width: 16, height: 16, factor: 0.95 },
  square14: { label: "خشتی ۱۴ × ۱۴", width: 14, height: 14, factor: 0.8 },
  octavo: { label: "اکتاو ۱۹ × ۱۶", width: 16, height: 19, factor: 1 },
  sultani: { label: "سلطانی", width: 24, height: 34, factor: 2.4 },
  square30: { label: "خشتی ۳۰ × ۳۰", width: 30, height: 30, factor: 2.8 },
  custom: { label: "قطع سفارشی", width: 14.5, height: 21.5, factor: 1 },
} as const;
export const textPrints: PrintOptions["textPrints"] = {
  "bw-text": {
    label: "سیاه و سفید (متنی)",
    bw: true,
    color: false,
    bwFactor: 1,
    colorFactor: 1,
  },
  "bw-image": {
    label: "سیاه و سفید (مصور)",
    bw: true,
    color: false,
    bwFactor: 1.35,
    colorFactor: 1,
  },
  "two-color": {
    label: "دو رنگ",
    bw: false,
    color: true,
    bwFactor: 1,
    colorFactor: 0.65,
  },
  "color-toner": {
    label: "چهار رنگ (تونری)",
    bw: false,
    color: true,
    bwFactor: 1,
    colorFactor: 1,
  },
  "color-ink": {
    label: "چهار رنگ (مرکبی)",
    bw: false,
    color: true,
    bwFactor: 1,
    colorFactor: 0.9,
  },
  "full-color": {
    label: "چهار رنگ (تمام رنگ)",
    bw: false,
    color: true,
    bwFactor: 1,
    colorFactor: 1.25,
  },
  "mixed-toner": {
    label: "ترکیبی (سیاه و سفید + رنگی تونری)",
    bw: true,
    color: true,
    bwFactor: 1,
    colorFactor: 1,
  },
  "mixed-two": {
    label: "ترکیبی (سیاه و سفید + دو رنگ)",
    bw: true,
    color: true,
    bwFactor: 1,
    colorFactor: 0.65,
  },
  "mixed-full": {
    label: "ترکیبی (سیاه و سفید + تمام رنگ)",
    bw: true,
    color: true,
    bwFactor: 1,
    colorFactor: 1.25,
  },
  "black-image": {
    label: "مصور مشکی",
    bw: true,
    color: false,
    bwFactor: 1.6,
    colorFactor: 1,
  },
  "mixed-bw-image-toner": {
    label: "ترکیبی (سیاه و سفید مصور + تونری)",
    bw: true,
    color: true,
    bwFactor: 1.35,
    colorFactor: 1,
  },
  "mixed-bw-image": {
    label: "ترکیبی (متنی + مصور مشکی)",
    bw: true,
    color: true,
    bwFactor: 1,
    colorFactor: 0.28,
  },
  "mixed-bw-image-full": {
    label: "ترکیبی (سیاه و سفید مصور + تمام رنگ)",
    bw: true,
    color: true,
    bwFactor: 1.35,
    colorFactor: 1.25,
  },
  "ink-image": {
    label: "چاپ مرکبی مصور",
    bw: false,
    color: true,
    bwFactor: 1,
    colorFactor: 1.2,
  },
  "toner-image": {
    label: "چاپ تونری مصور",
    bw: false,
    color: true,
    bwFactor: 1,
    colorFactor: 1.35,
  },
  "two-image": {
    label: "چاپ دو رنگ مصور",
    bw: false,
    color: true,
    bwFactor: 1,
    colorFactor: 0.85,
  },
  "mixed-toner-image": {
    label: "ترکیبی (سیاه و سفید + تونری مصور)",
    bw: true,
    color: true,
    bwFactor: 1,
    colorFactor: 1.35,
  },
  manga: {
    label: "سیاه و سفید (مانگا)",
    bw: true,
    color: false,
    bwFactor: 1.8,
    colorFactor: 1,
  },
  "no-print": {
    label: "بدون چاپ متن",
    bw: true,
    color: false,
    bwFactor: 0,
    colorFactor: 0,
  },
  "bw-full": {
    label: "سیاه و سفید (پوشش کامل)",
    bw: true,
    color: false,
    bwFactor: 1.8,
    colorFactor: 1,
  },
} as const;
export const bookPapers: PrintOptions["bookPapers"] = {
  white70: {
    label: "تحریر سفید ۷۰ گرم",
    grams: 70,
    type: "white",
    caliper: 0.09,
  },
  white80: {
    label: "تحریر سفید ۸۰ گرم",
    grams: 80,
    type: "white",
    caliper: 0.103,
  },
  white100: {
    label: "تحریر سفید ۱۰۰ گرم",
    grams: 100,
    type: "white",
    caliper: 0.128,
  },
  cream60: {
    label: "تحریر نخودی ۶۰ گرم",
    grams: 60,
    type: "cream",
    caliper: 0.1,
  },
  bulky: { label: "بالکی کرم", grams: 60, type: "cream", caliper: 0.12 },
  colored: { label: "تحریر الوان", grams: 80, type: "colored", caliper: 0.105 },
  coated128: {
    label: "گلاسه براق ۱۲۸ گرم",
    grams: 128,
    type: "coated",
    caliper: 0.11,
  },
  coated200: {
    label: "گلاسه براق ۲۰۰ گرم",
    grams: 200,
    type: "coated",
    caliper: 0.172,
  },
  coated250: {
    label: "گلاسه براق ۲۵۰ گرم",
    grams: 250,
    type: "coated",
    caliper: 0.215,
  },
  coated300: {
    label: "گلاسه براق ۳۰۰ گرم",
    grams: 300,
    type: "coated",
    caliper: 0.258,
  },
  label: {
    label: "کاغذ پشت‌چسب‌دار",
    grams: 160,
    type: "label",
    caliper: 0.18,
  },
} as const;
export const bookBindings: PrintOptions["bookBindings"] = {
  "hot-glue": {
    label: "چسب گرم (شومیز)",
    base: "soft",
    factor: 1,
    hard: false,
  },
  "double-wire": {
    label: "فنر دوبل",
    base: "spiral",
    factor: 1.2,
    hard: false,
  },
  spiral: { label: "فنر مارپیچ", base: "spiral", factor: 1, hard: false },
  staple: { label: "منگنه تخت", base: "soft", factor: 0.45, hard: false },
  loop: { label: "منگنه لوپ", base: "soft", factor: 0.65, hard: false },
  "hard-flat": {
    label: "جلد سخت عطف تخت",
    base: "hard",
    factor: 1,
    hard: true,
  },
  "hard-round": {
    label: "جلد سخت عطف گرد",
    base: "hard",
    factor: 1.25,
    hard: true,
  },
  "hard-spiral": {
    label: "جلد سخت + فنری",
    base: "hard",
    factor: 1,
    hard: true,
  },
} as const;
export const coverPrints: PrintOptions["coverPrints"] = {
  front: "چهار رنگ یک رو",
  both: "چهار رنگ دو رو",
  flap: "چهار رنگ یک رو با لبه برگردان",
  "both-flap": "چهار رنگ دو رو با لبه برگردان",
  none: "بدون جلد",
  plastic: "طلق",
} as const;
export const coverPapers: PrintOptions["coverPapers"] = {
  coated300: "گلاسه ۳۰۰ گرم",
  coated250: "گلاسه ۲۵۰ گرم",
  coated200: "گلاسه ۲۰۰ گرم",
  kraft270: "کرافت ۲۷۰ گرم",
  plastic: "طلق طرح‌دار",
} as const;
export const coverCoatings: PrintOptions["coverCoatings"] = {
  gloss: "سلفون براق",
  matte: "سلفون مات",
  none: "بدون سلفون",
} as const;
export const finishingServices: PrintOptions["finishingServices"] = {
  roundCorners: { label: "گوشه گرد", matte: false, hard: false },
  shrink: { label: "شیرینک", matte: false, hard: false },
  goldFoil: { label: "طلاکوب", matte: true, hard: false },
  silverFoil: { label: "نقره‌کوب", matte: true, hard: false },
  spotUV: { label: "یووی موضعی", matte: true, hard: false },
  endpaperBw: { label: "آستر بدرقه سیاه و سفید", matte: false, hard: true },
  endpaperColor: { label: "آستر بدرقه رنگی", matte: false, hard: true },
  bookmark: { label: "نشانگر کتاب", matte: false, hard: false },
  coloredEdges: { label: "لبه رنگی", matte: false, hard: false },
  metallicEdges: { label: "لبه طلایی / نقره‌ای", matte: false, hard: false },
  bellyBand: { label: "بلی‌بند", matte: false, hard: false },
  thickEdges: { label: "لبه رنگی قطور", matte: false, hard: false },
  hardCoverJacket: { label: "کاور جلد سخت", matte: false, hard: true },
  ribbon: { label: "روبان جلد سخت", matte: false, hard: true },
  laminatedEndpaper: {
    label: "آستر بدرقه رنگی + سلفون",
    matte: false,
    hard: true,
  },
} as const;
export type BookDetails = {
  width: number;
  height: number;
  printType: string;
  sides: "double" | "single";
  bwPages: number;
  colorPages: number;
  bwPaper: string;
  colorPaper: string;
  printOnly: boolean;
  bindingStyle: string;
  coverPrint: string;
  coverPaper: string;
  coating: string;
  orientation: "portrait" | "landscape";
  readingDirection: "rtl" | "ltr";
  extras: string[];
};
