import type { Conditions } from "./final-conditions";

export function nextOrderAction(status:string, correction?:string, terms?: Conditions | null, priceState?:string) {
  if (status === "cancelled") return "این سفارش لغو شده است.";
  if (["review","confirmed"].includes(status)) {
    if (correction === "requested") return "مشتری باید PDF اصلاح‌شده را ارسال کند.";
    if (correction === "submitted") return "چاپخانه باید نسخهٔ اصلاح‌شده را بازبینی کند.";
    if (correction === "rejected") return "چاپخانه باید اصلاحیهٔ بعدی را درخواست کند.";
    if (priceState === "accepted") return terms?.acceptedAt ? "چاپخانه می‌تواند آمادگی تولید را بررسی و چاپ را شروع کند."
      : terms ? "مشتری باید شرایط نهایی را بررسی کند." : "چاپخانه باید شرایط نهایی چاپ و تحویل را تنظیم کند.";
    if (priceState === "revision_requested") return "چاپخانه باید پیشنهاد قیمت را بر اساس درخواست بازنگری بررسی کند.";
  }
  const actions:Record<string,string> = {
    new:"درخواست در نوبت بررسی چاپخانه است.", review:"چاپخانه مشخصات و فایل را بررسی و قیمت را پیشنهاد می‌کند.",
    awaiting_customer_approval:"مشتری باید دربارهٔ آخرین پیشنهاد قیمت تصمیم بگیرد.", printing:"چاپخانه در حال چاپ سفارش است.",
    binding:"سفارش در مرحلهٔ صحافی است.", ready:"سفارش آمادهٔ تحویل است.", completed:"تحویل سفارش ثبت شده است.",
  };
  return actions[status];
}

export default function OrderProgress({status, accepted = false, binding = true, terms}: {
  status: string; accepted?: boolean; binding?: boolean; terms?: Conditions | null;
}) {
  const steps = [
    ["new","درخواست ثبت شد"], ["review","بررسی سفارش"], ["price","قیمت و تأیید مشتری"],
    ["terms","شرایط نهایی چاپ و تحویل"], ["final","تأیید نهایی مشتری"], ["printing","در صف / حال چاپ"],
    ...(binding || status === "binding" ? [["binding","صحافی"]] : []),
    ["ready","آماده تحویل"], ["completed","تکمیل‌شده"],
  ];
  if (status === "cancelled") return null;
  const stage = status === "awaiting_customer_approval" ? "price" : status === "confirmed"
    ? terms ? "final" : "terms" : status === "review" && accepted ? "terms" : status;
  const current = steps.findIndex(([key])=>key === stage);
  return <ol className="workflow-progress" aria-label="مراحل سفارش">{steps.map(([key,label], index) =>
    <li key={key} aria-current={index === current ? "step" : undefined} data-complete={index < current}>{label}</li>)}</ol>;
}
