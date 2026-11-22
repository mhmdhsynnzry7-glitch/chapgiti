import { z } from "zod";
import { normalizeDigits } from "./pricing";
const count = (min: number, max: number) =>
  z.preprocess((value) => {
    if (typeof value !== "string") return value;
    const text = normalizeDigits(value.trim());
    return /^[0-9]{1,16}$/.test(text) ? Number(text) : Number.NaN;
  }, z.number().int().min(min).max(max));
import {
  type PrintOptions,
  bookSizes,
  textPrints,
  bookPapers,
  bookBindings,
  coverPrints,
  coverPapers,
  coverCoatings,
  finishingServices,
} from "./book-options";
const keys = <T extends Record<string, unknown>>(value: T) =>
  Object.keys(value) as [keyof T & string, ...(keyof T & string)[]];
export function createPrintSpecSchema(options: PrintOptions) {
  const {
    bookSizes,
    textPrints,
    bookPapers,
    bookBindings,
    coverPrints,
    coverPapers,
    coverCoatings,
    finishingServices,
  } = options;
  const bookDetailsSchema = z.object({
    width: z.number().min(1).max(100),
    height: z.number().min(1).max(100),
    printType: z.enum(keys(textPrints)),
    sides: z.enum(["double", "single"]),
    bwPages: count(0, 2000),
    colorPages: count(0, 2000),
    bwPaper: z.enum(keys(bookPapers)),
    colorPaper: z.enum(keys(bookPapers)),
    printOnly: z.boolean(),
    bindingStyle: z.enum(keys(bookBindings)),
    coverPrint: z.enum(keys(coverPrints)),
    coverPaper: z.enum(keys(coverPapers)),
    coating: z.enum(keys(coverCoatings)),
    orientation: z.enum(["portrait", "landscape"]),
    readingDirection: z.enum(["rtl", "ltr"]),
    extras: z
      .array(z.enum(keys(finishingServices)))
      .max(Object.keys(finishingServices).length),
  });
  return z
    .object({
      pages: count(4, 2000),
      quantity: count(1, 10000),
      size: z.enum(keys(bookSizes)),
      paper: z.enum(["white", "cream", "coated"]),
      color: z.enum(["bw", "color"]),
      binding: z.enum(["soft", "hard", "spiral"]),
      details: bookDetailsSchema.optional(),
    })
    .superRefine((spec, ctx) => {
      const d = spec.details;
      if (!d) {
        if (!["raghi", "vaziri", "a4"].includes(spec.size))
          ctx.addIssue({
            code: "custom",
            message: "برای قطع انتخاب‌شده، مشخصات کامل را وارد کنید.",
          });
        return;
      }
      const error = (message: string) =>
        ctx.addIssue({ code: "custom", message, path: ["details"] });
      if (d.bwPages + d.colorPages !== spec.pages)
        error("جمع صفحات رنگی و سیاه و سفید معتبر نیست.");
      const mode = textPrints[d.printType];
      if ((!mode.bw && d.bwPages > 0) || (!mode.color && d.colorPages > 0))
        error("تعداد صفحات با نوع چاپ انتخاب‌شده سازگار نیست.");
      if (bookBindings[d.bindingStyle].base !== spec.binding)
        error("نوع صحافی با مشخصات سفارش سازگار نیست.");
      if (
        !(bookSizes[spec.size].isCustom ?? spec.size === "custom") &&
        (Math.abs(d.width - bookSizes[spec.size].width) > 0.01 ||
          Math.abs(d.height - bookSizes[spec.size].height) > 0.01)
      )
        error("ابعاد با قطع انتخاب‌شده سازگار نیست.");
      if (bookSizes[spec.size].isCustom ?? spec.size === "custom") {
        const format = bookSizes[spec.size];
        if (
          d.width < (format.minWidth ?? 8) ||
          d.width > (format.maxWidth ?? 45) ||
          d.height < (format.minHeight ?? 8) ||
          d.height > (format.maxHeight ?? 45)
        )
          error("ابعاد خارج از بازه مجاز قطع انتخاب‌شده است.");
      }
      if (new Set(d.extras).size !== d.extras.length)
        error("خدمات تکمیلی تکراری است.");
      if (d.printOnly && d.extras.length)
        error("خدمات تکمیلی جلد با گزینه فقط چاپ قابل انتخاب نیست.");
      const cp = coverPrints[d.coverPrint],
        coating = coverCoatings[d.coating];
      const noCover =
          typeof cp === "object" ? cp.none : d.coverPrint === "none",
        plastic =
          typeof cp === "object" ? cp.plastic : d.coverPrint === "plastic",
        matte =
          typeof coating === "object" ? coating.matte : d.coating === "matte";
      if (!d.printOnly && noCover && d.extras.length)
        error("برای خدمات تکمیلی، جلد را انتخاب کنید.");
      for (const key of d.extras) {
        if (finishingServices[key].matte && (!matte || plastic))
          error("طلاکوب، نقره‌کوب و یووی موضعی به سلفون مات نیاز دارند.");
        if (finishingServices[key].hard && !bookBindings[d.bindingStyle].hard)
          error("خدمت انتخاب‌شده مخصوص جلد سخت است.");
      }
    });
}
export const printSpecSchema = createPrintSpecSchema({
  bookSizes,
  textPrints,
  bookPapers,
  bookBindings,
  coverPrints,
  coverPapers,
  coverCoatings,
  finishingServices,
});
const code = z.string().regex(/^[a-zA-Z0-9][a-zA-Z0-9_-]{0,79}$/);
// Drafts carry only technical values. The SQL catalog is checked again before submission.
export const draftPrintSpecSchema = z.object({
  pages: count(4, 2000),
  quantity: count(1, 10000),
  size: code,
  paper: z.enum(["white", "cream", "coated"]),
  color: z.enum(["bw", "color"]),
  binding: z.enum(["soft", "hard", "spiral"]),
  details: z
    .object({
      width: z.number().min(1).max(100),
      height: z.number().min(1).max(100),
      printType: code,
      sides: z.enum(["double", "single"]),
      bwPages: count(0, 2000),
      colorPages: count(0, 2000),
      bwPaper: code,
      colorPaper: code,
      printOnly: z.boolean(),
      bindingStyle: code,
      coverPrint: code,
      coverPaper: code,
      coating: code,
      orientation: z.enum(["portrait", "landscape"]),
      readingDirection: z.enum(["rtl", "ltr"]),
      extras: z.array(code).max(15),
    })
    .optional(),
});
export function validatePrintSpec(value: unknown, options?: PrintOptions) {
  const result = (
    options ? createPrintSpecSchema(options) : printSpecSchema
  ).safeParse(value);
  if (result.success) return result.data;
  const issue = result.error.issues[0];
  if (issue.path[0] === "pages")
    throw new Error("مجموع صفحات هر جلد باید یک عدد صحیح بین ۴ و ۲۰۰۰ باشد.");
  if (issue.path[0] === "quantity")
    throw new Error("تیراژ باید یک عدد صحیح بین ۱ و ۱۰٬۰۰۰ باشد.");
  if (issue.path.includes("bwPages") || issue.path.includes("colorPages"))
    throw new Error(
      "تعداد صفحات رنگی و سیاه‌وسفید باید عدد صحیح بین صفر و ۲۰۰۰ باشد.",
    );
  throw new Error(
    issue.code === "custom"
      ? issue.message
      : "مشخصات چاپ را کامل و بررسی کنید.",
  );
}
