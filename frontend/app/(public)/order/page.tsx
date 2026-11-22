import { normalizeDigits, type PrintSpec } from "@/lib/pricing";
import PageIntro from "@/components/page-intro";
import { OrderForm } from "@/components/forms";
import { requireCustomerProfile } from "@/lib/customer";
import { draftPrintSpecSchema } from "@/lib/print-spec";
export const metadata = {
  title: "ثبت سفارش چاپ کتاب",
  robots: { index: false, follow: true },
};
export default async function Page({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const p = await searchParams,
    overrides: Partial<PrintSpec> = {},
    query = new URLSearchParams();
  let initialSpec: PrintSpec | undefined;
  if (
    typeof p.size === "string" &&
    /^[a-zA-Z0-9][a-zA-Z0-9_-]{0,79}$/.test(p.size) &&
    !["constructor", "prototype", "__proto__"].includes(p.size)
  ) {
    overrides.size = p.size;
    query.set("size", p.size);
  }
  for (const [key, values] of [
    ["paper", ["white", "cream", "coated"]],
    ["color", ["bw", "color"]],
    ["binding", ["soft", "hard", "spiral"]],
  ] as const) {
    const value = p[key];
    if (
      typeof value === "string" &&
      (values as readonly string[]).includes(value)
    ) {
      Object.assign(overrides, { [key]: value });
      query.set(key, value);
    }
  }
  for (const key of ["pages", "quantity"] as const) {
    const value = p[key];
    if (typeof value !== "string") continue;
    const text = normalizeDigits(value),
      number = Number(text);
    if (
      /^\d+$/.test(text) &&
      Number.isInteger(number) &&
      number >= (key === "pages" ? 4 : 1) &&
      number <= (key === "pages" ? 2000 : 10000)
    ) {
      overrides[key] = number;
      query.set(key, String(number));
    }
  }
  if (typeof p.spec === "string" && p.spec.length <= 4000) {
    try {
      const parsed = draftPrintSpecSchema.safeParse(JSON.parse(p.spec));
      if (parsed.success) {
        initialSpec = parsed.data;
        query.set("spec", JSON.stringify(parsed.data));
      }
    } catch {}
  }
  if (p.draft === "1") query.set("draft", "1");
  await requireCustomerProfile(
    "/order" + (query.size ? "?" + query.toString() : ""),
  );
  return (
    <div className="container page-body">
      <PageIntro
        title="شروع داستان چاپ شما"
        description="مشخصات کتاب را انتخاب و فایل چاپ را ارسال کنید. پس از ثبت، کد پیگیری دریافت می‌کنید و کارشناس درخواست شما را بررسی می‌کند."
        eyebrow="ثبت سفارش چاپ و نشر"
      />
      <OrderForm
        initialSpec={initialSpec}
        initialOverrides={overrides}
        restoreDraft={p.draft === "1"}
      />
    </div>
  );
}
